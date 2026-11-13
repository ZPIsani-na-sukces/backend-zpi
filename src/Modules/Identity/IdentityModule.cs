using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zpi.Database;
using Zpi.Modules.Identity.Api;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Infrastructure;
using Zpi.Modules.Identity.Application;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.BackgroundJobs;
using Zpi.Modules.Identity.Infrastructure.Email;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.Identity.Infrastructure.Redis;
using Zpi.Modules.Identity.Infrastructure.Sms;

namespace Zpi.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<TimeProvider>(new PostgresTimeProvider());
        services.AddIdentitySms(configuration);
        services.AddIdentityEmail(configuration, environment);
        services.AddScoped<IIdentityFacade, IdentityFacade>();
        services.AddIdentityApplication();
        services.AddIdentityApi();
        services.AddIdentityAuthentication(configuration);
        services.AddIdentityRedis(configuration);
        services.AddIdentityRateLimiting();
        services.AddIdentityBackgroundJobs(configuration);
        services.AddIdentityPersistence(configuration);
        return services;
    }
}
