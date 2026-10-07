using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;

namespace Zpi.Modules.Identity.Infrastructure.Persistence;

public static class PersistenceSetup
{
    public static IServiceCollection AddIdentityPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<IdentityDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, IdentityDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<IdentityDbContext>>("Identity", tags: ["ready"]);
        return services;
    }
}
