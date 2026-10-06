using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Modules.Safety.Persistence;

namespace Zpi.Modules.Safety;

public static class SafetyModule
{
    public static IServiceCollection AddSafetyModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<SafetyDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, SafetyDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<SafetyDbContext>>("Safety", tags: ["ready"]);
        return services;
    }
}
