// ASP.NET Core implicit usings (Web SDK ulardan farqli, bizga qo'lda kerak)
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;

// Domain entities
global using SecureGate.Domain;
global using SecureGate.Domain.Auth;
global using SecureGate.Domain.Access;
global using SecureGate.Domain.Cameras;
global using SecureGate.Domain.People;
global using SecureGate.Domain.Common;

// ViewModels
global using SecureGate.Infrastructure.ViewModels;
global using SecureGate.Infrastructure.ViewModels.Auth;
global using SecureGate.Infrastructure.ViewModels.Admin;
global using SecureGate.Infrastructure.ViewModels.Cameras;
global using SecureGate.Infrastructure.ViewModels.People;
global using SecureGate.Infrastructure.ViewModels.Access;
global using SecureGate.Infrastructure.ViewModels.Dashboard;
global using SecureGate.Infrastructure.ViewModels.Reports;
global using SecureGate.Infrastructure.ViewModels.Settings;
global using SecureGate.Infrastructure.ViewModels.Shared;
