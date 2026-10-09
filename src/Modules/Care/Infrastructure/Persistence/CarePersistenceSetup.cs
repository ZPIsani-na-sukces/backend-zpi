using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;

namespace Zpi.Modules.Care.Infrastructure.Persistence;

public static class CarePersistenceSetup
{
    public static IServiceCollection AddCarePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<CareDbContext>(options => DatabaseConfiguration.Configure(options, connectionString, CareDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<CareDbContext>>("Care", tags: ["ready"]);
        return services;
    }
}
