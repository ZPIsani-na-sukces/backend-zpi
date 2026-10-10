using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.SeniorProfile.Api;
using Zpi.Modules.SeniorProfile.Application;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile;

public static class SeniorProfileModule
{
    public static IServiceCollection AddSeniorProfileModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSeniorProfileApi();
        services.AddSeniorProfileApplication();
        services.AddSeniorProfilePersistence(configuration);
        return services;
    }
}
