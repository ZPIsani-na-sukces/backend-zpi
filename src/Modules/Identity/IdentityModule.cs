using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Modules.Identity.Persistence;

namespace Zpi.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<IdentityDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, IdentityDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<IdentityDbContext>>("Identity", tags: ["ready"]);
        return services;
    }
}
