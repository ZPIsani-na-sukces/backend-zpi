using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Safety.Api;
using Zpi.Modules.Safety.Application;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety;

public static class SafetyModule
{
    public static IServiceCollection AddSafetyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSafetyApi();
        services.AddSafetyApplication();
        services.AddSafetyPersistence(configuration);
        return services;
    }
}
