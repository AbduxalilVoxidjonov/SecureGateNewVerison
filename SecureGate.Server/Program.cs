using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SecureGate.Api.Auth;
using SecureGate.Api.Filters;
using SecureGate.Data;
using SecureGate.Domain.Auth;
using SecureGate.Infrastructure.Hubs;
using SecureGate.Infrastructure.Services.Implementations;
using SecureGate.Infrastructure.Services.Interfaces;
using System.Security;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ===== Controllers + JSON =====
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// ===== SignalR =====
builder.Services.AddSignalR();

// ===== Database =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== Identity =====
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ===== JWT settings =====
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSection);
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

// ===== Authentication (Cookie + JWT) =====
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                // SignalR hublari va <img> orqali ochiladigan kamera media (stream/snapshot)
                // uchun token query-string'dan ham qabul qilinadi (img/EventSource header yubora olmaydi).
                var isMedia = path.StartsWithSegments("/hubs")
                    || (path.HasValue && (path.Value.EndsWith("/stream") || path.Value.EndsWith("/snapshot")));
                if (!string.IsNullOrEmpty(accessToken) && isMedia)
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "SecureGate.Auth";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// ===== Authorization (permission policies) =====
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            JwtBearerDefaults.AuthenticationScheme,
            IdentityConstants.ApplicationScheme)
        .RequireAuthenticatedUser()
        .Build();

    options.FallbackPolicy = options.DefaultPolicy;

    foreach (var permission in Enum.GetValues<Permission>())
    {
        options.AddPolicy(HasPermissionAttribute.PolicyName(permission), policy =>
        {
            policy.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
            policy.AuthenticationSchemes.Add(IdentityConstants.ApplicationScheme);
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new PermissionRequirement(permission));
        });
    }
});

// ===== Token service =====
builder.Services.AddScoped<ITokenService, TokenService>();

// ===== Services (DI) =====
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<ITurnstileService, TurnstileService>();
builder.Services.AddScoped<ICameraService, CameraService>();
builder.Services.AddScoped<IAccessLogService, AccessLogService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISettingService, SettingService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
builder.Services.AddScoped<ICameraUserService, CameraUserService>();
builder.Services.AddSingleton<ICameraCredentialProtector, CameraCredentialProtector>();
builder.Services.AddScoped<IDeviceConnectionTester, DeviceConnectionTester>();
builder.Services.AddScoped<ICameraMjpegStreamer, CameraMjpegStreamer>();

// ===== Face Recognition =====
builder.Services.AddSingleton<IFaceRecognitionEngine, FaceRecognitionEngine>();
builder.Services.AddSingleton<IKnownFaceCache, KnownFaceCache>();
builder.Services.AddScoped<IFaceRecognitionClient, FaceRecognitionClient>();
builder.Services.AddScoped<IFaceMatchHandler, FaceMatchHandler>();
builder.Services.AddScoped<ICameraSightingHandler, CameraSightingHandler>();
builder.Services.AddHostedService<CameraStreamWorker>();

// ===== CORS =====
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

// ===== Swagger =====
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SecureGate API",
        Version = "v1",
        Description = "SecureGate yuz tanish va kirish boshqaruv tizimi uchun REST API.\n\n" +
                      "Autentifikatsiya: JWT Bearer yoki Identity Cookie. " +
                      "JWT olish uchun `/api/auth/login` ga POST yuboring.",
        Contact = new OpenApiContact { Name = "SecureGate" }
    });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Bearer {token} formatida JWT kiriting",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(jwtScheme.Reference.Id, jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });

    c.EnableAnnotations();
});

var app = builder.Build();

// ===== Seed Database =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    context.Database.Migrate();

    var userManager = services.GetRequiredService<UserManager<AppUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    await IdentitySeeder.SeedAsync(userManager, roleManager);
}

// ===== Middleware =====
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SecureGate API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ===== SignalR Hubs =====
app.MapHub<TurnstileHub>("/hubs/turnstile");
app.MapHub<CameraHub>("/hubs/camera");
app.MapHub<AlertHub>("/hubs/alert");
app.MapHub<DashboardHub>("/hubs/dashboard");

app.Run();
