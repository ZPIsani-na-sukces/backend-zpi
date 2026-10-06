using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Modules.Care.Persistence;

namespace Zpi.Modules.Care;

public static class CareModule
{
    public static IServiceCollection AddCareModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<CareDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, CareDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<CareDbContext>>("Care", tags: ["ready"]);
        return services;
    }
}
