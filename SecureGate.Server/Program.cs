using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
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
using SecureGate.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// OpenCV/FFMPEG global sozlamalari (RTSP past-kechikish flaglari, TCP transport, timeout).
// Har qanday VideoCapture ochilishidan OLDIN, bir marta o'rnatiladi — VideoCapture faqat
// hosted service'lar/so'rovlar boshlangach ochiladi, ya'ni bu yer xavfsiz.
// Konfiguratsiyadan `Camera:FfmpegOptions` orqali override qilinadi.
OpenCvBootstrap.Configure(builder.Configuration);

// ===== Controllers + JSON =====
builder.Services.AddControllers(options =>
{
    // DB butunligi buzilishi (unique index / foreign key) bo'sh 500 emas,
    // tushunarli 409/400 + ApiResponse bo'lib qaytsin.
    options.Filters.Add<DbConstraintExceptionFilter>();
})
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // [ApiController] ning avtomatik 400'i ham ApiResponse shaklida bo'lsin —
        // aks holda klient {success, message, errors} o'rniga ProblemDetails oladi.
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(kvp => kvp.Value is not null && kvp.Value.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

            return new BadRequestObjectResult(ApiResponse.Fail("Validatsiya xatosi", errors));
        };
    });

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();

// ===== SignalR =====
builder.Services.AddSignalR();

// ===== Database =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(maxRetryCount: 10,
                                        maxRetryDelay: TimeSpan.FromSeconds(10),
                                        errorNumbersToAdd: null)));

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

        // Brute-force himoyasi: login'da CheckPasswordSignInAsync(lockoutOnFailure: true) ishlatiladi.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// ===== JWT settings =====
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSection);
var jwt = jwtSection.Get<JwtSettings>() ?? new JwtSettings();

// Kalit yo'q yoki 32 baytdan qisqa bo'lsa ilova ishga tushmasin — aks holda
// imzoni brute-force qilib SuperAdmin token yasash mumkin bo'lib qoladi.
jwt.Validate();

// Cookie sxemasi uchun SecurityStamp tekshiruvi (default 30 daqiqa juda uzoq).
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
    o.ValidationInterval = TimeSpan.FromMinutes(2));

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
                    || (path.HasValue && (path.Value.EndsWith("/stream")
                                          || path.Value.EndsWith("/snapshot")
                                          || path.Value.EndsWith("/download")));
                if (!string.IsNullOrEmpty(accessToken) && isMedia)
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            // Logout, parol o'zgarishi va akkauntni bloklash eski access tokenni
            // DARHOL bekor qilsin (JWT stateless bo'lgani uchun SecurityStamp orqali).
            OnTokenValidated = async ctx =>
            {
                var userManager = ctx.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();

                var userId = ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? ctx.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    ctx.Fail("Token noto'g'ri.");
                    return;
                }

                var user = await userManager.FindByIdAsync(userId);
                if (user is null || !user.IsActive)
                {
                    ctx.Fail("Akkaunt mavjud emas yoki bloklangan.");
                    return;
                }

                var tokenStamp = ctx.Principal?.FindFirst(SecureGateClaims.SecurityStamp)?.Value;
                var currentStamp = await userManager.GetSecurityStampAsync(user);
                if (string.IsNullOrEmpty(tokenStamp)
                    || !string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal))
                {
                    ctx.Fail("Token bekor qilingan. Qaytadan tizimga kiring.");
                }
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

// ===== Data Protection =====
// Konteynerda standart key ring konteyner ichidagi vaqtinchalik katalogda yotadi va
// qayta ishga tushganda yo'qoladi — natijada CameraCredentialProtector bilan
// shifrlangan kamera parollari ochilmay qoladi. `DataProtection:KeyRingPath`
// berilgan bo'lsa kalitlar doimiy diskda (Docker volume) saqlanadi.
var keyRingPath = builder.Configuration["DataProtection:KeyRingPath"];
if (!string.IsNullOrWhiteSpace(keyRingPath))
{
    Directory.CreateDirectory(keyRingPath);
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
        .SetApplicationName("SecureGate");
}

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
// Singleton bo'lishi SHART: kamera bo'yicha bitta RTSP ulanish + ko'p mijozga fan-out
// shu instansiyadagi broadcaster lug'atida saqlanadi. Scoped bo'lsa har so'rovda
// yangi lug'at yaratilib, har mijoz yana alohida RTSP sessiya ochadi.
builder.Services.AddSingleton<ICameraMjpegStreamer, CameraMjpegStreamer>();

// ===== NVR: oqim URL qurish va arxiv (eski yozuvlar) =====
// StreamUrlBuilder holatsiz — vendor (Hikvision/Dahua/Axis) va NVR kanal raqamiga
// qarab RTSP URL quradi. Foydalanuvchi to'liq URL kiritgan bo'lsa uni o'zgartirmaydi.
builder.Services.AddSingleton<IStreamUrlBuilder, StreamUrlBuilder>();

// Hikvision ISAPI uchun named client — faqat connection pooling va timeout uchun.
// Digest autentifikatsiyasi so'rov darajasida hisoblanadi: handler umumiy bo'lgani uchun
// unga bitta NetworkCredential qo'yib bo'lmaydi (aks holda NVR'lar bir-birining
// parolini ishlatib yuborardi).
builder.Services.AddHttpClient(HikvisionNvrArchiveService.HttpClientName)
    .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(30));

builder.Services.AddSingleton<HikvisionNvrArchiveService>();

// Router vendorlarni OCHIQ ro'yxat bilan oladi — DI enumerable EMAS, chunki router
// o'zi ham INvrArchiveService bo'lgani uchun o'zini o'ziga qo'shib rekursiya hosil qilardi.
// Yangi vendor qo'shish = quyidagi massivga bitta qator.
builder.Services.AddSingleton<NvrArchiveRouter>(sp => new NvrArchiveRouter(
    new INvrArchiveService[]
    {
        sp.GetRequiredService<HikvisionNvrArchiveService>()
    },
    sp.GetRequiredService<ILogger<NvrArchiveRouter>>()));

builder.Services.AddSingleton<INvrArchiveService>(sp => sp.GetRequiredService<NvrArchiveRouter>());
builder.Services.AddSingleton<INvrArchiveResolver>(sp => sp.GetRequiredService<NvrArchiveRouter>());

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

// ===== Rate limiting =====
// Faqat global limiter ishlatiladi (controllerlarda [EnableRateLimiting] yo'q),
// shuning uchun "policy not found" xatosi bo'lishi mumkin emas.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path;

        // SignalR hublari va uzluksiz media oqimlari cheklanmaydi.
        if (path.StartsWithSegments("/hubs")
            || (path.HasValue && (path.Value.EndsWith("/stream")
                                  || path.Value.EndsWith("/snapshot")
                                  || path.Value.EndsWith("/download"))))
        {
            return RateLimitPartition.GetNoLimiter("nolimit");
        }

        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Auth endpointlari — qat'iy (login brute-force / refresh spam).
        if (path.StartsWithSegments("/api/auth"))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
        }

        return RateLimitPartition.GetFixedWindowLimiter($"api:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 300,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.ContentType = "application/json";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            ApiResponse.Fail("Juda ko'p so'rov. Birozdan so'ng qayta urinib ko'ring."), ct);
    };
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
// Konteynerda SQL Server app'dan keyinroq tayyor bo'lishi mumkin (healthcheck bo'lsa ham
// login endpoint'i bir necha soniya "not ready" qaytaradi), shuning uchun retry bilan urinamiz.
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var maxAttempts = app.Configuration.GetValue<int?>("Startup:MigrationRetryCount") ?? 20;
    var delay = TimeSpan.FromSeconds(app.Configuration.GetValue<int?>("Startup:MigrationRetryDelaySeconds") ?? 5);

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<AppDbContext>();
            context.Database.Migrate();

            var userManager = services.GetRequiredService<UserManager<AppUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            await IdentitySeeder.SeedAsync(userManager, roleManager, app.Configuration, startupLogger);
            startupLogger.LogInformation("Ma'lumotlar bazasi migratsiyasi va seed muvaffaqiyatli (urinish {Attempt}).", attempt);
            break;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            startupLogger.LogWarning(ex,
                "Ma'lumotlar bazasiga ulanib bo'lmadi (urinish {Attempt}/{Max}). {Delay}s dan keyin qayta urinamiz.",
                attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
        }
    }
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

// Konteynerda faqat HTTP listener bor — bunday holda UseHttpsRedirection()
// 307 loop / "Failed to determine the https port" xatosini beradi, shuning uchun o'tkazib yuboramiz.
var listenUrls = app.Configuration["ASPNETCORE_URLS"] ?? app.Configuration["urls"];
var httpsConfigured = string.IsNullOrWhiteSpace(listenUrls)
    || listenUrls.Contains("https://", StringComparison.OrdinalIgnoreCase)
    || !string.IsNullOrWhiteSpace(app.Configuration["ASPNETCORE_HTTPS_PORT"])
    || !string.IsNullOrWhiteSpace(app.Configuration["HTTPS_PORT"]);

if (httpsConfigured)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();

app.UseRateLimiter();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ===== SignalR Hubs =====
// RequireAuthorization() — hub sinflaridagi [Authorize] ustiga qo'shimcha himoya:
// anonim klient ulanib turniket ocha olmasligi kerak.
app.MapHub<TurnstileHub>("/hubs/turnstile").RequireAuthorization();
app.MapHub<CameraHub>("/hubs/camera").RequireAuthorization();
app.MapHub<AlertHub>("/hubs/alert").RequireAuthorization();
app.MapHub<DashboardHub>("/hubs/dashboard").RequireAuthorization();

// ===== SPA fallback =====
// React Router uchun: wwwroot'da mos statik fayl topilmasa index.html qaytariladi.
// MapFallbackToFile eng past route prioritetiga ega, shuning uchun /api/* va /hubs/*
// route'lariga xalaqit bermaydi; /swagger esa routing'dan oldingi middleware.
// AllowAnonymous — global FallbackPolicy (RequireAuthenticatedUser) index.html'ni bloklamasligi uchun.
app.MapFallbackToFile("index.html").AllowAnonymous();

app.Run();
