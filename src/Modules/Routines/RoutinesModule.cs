using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Modules.Routines.Persistence;

namespace Zpi.Modules.Routines;

public static class RoutinesModule
{
    public static IServiceCollection AddRoutinesModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<RoutinesDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, RoutinesDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<RoutinesDbContext>>("Routines", tags: ["ready"]);
        return services;
    }
}
