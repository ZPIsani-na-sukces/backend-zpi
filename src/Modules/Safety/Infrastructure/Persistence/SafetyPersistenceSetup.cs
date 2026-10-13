using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;

namespace Zpi.Modules.Safety.Infrastructure.Persistence;

public static class SafetyPersistenceSetup
{
    public static IServiceCollection AddSafetyPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<SafetyDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, SafetyDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<SafetyDbContext>>("Safety", tags: ["ready"]);
        return services;
    }
}
