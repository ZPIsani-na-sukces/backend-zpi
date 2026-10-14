using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;

namespace Zpi.Modules.Routines.Infrastructure.Persistence;

public static class RoutinesPersistenceSetup
{
    public static IServiceCollection AddRoutinesPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<RoutinesDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, RoutinesDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<RoutinesDbContext>>("Routines", tags: ["ready"]);
        return services;
    }
}
