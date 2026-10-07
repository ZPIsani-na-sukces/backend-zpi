using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Api;
using Zpi.Modules.Identity.Application;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.BackgroundJobs;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.Identity.Infrastructure.Redis;

namespace Zpi.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
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
