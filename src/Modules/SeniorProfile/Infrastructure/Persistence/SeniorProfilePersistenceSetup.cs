using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;

namespace Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

public static class SeniorProfilePersistenceSetup
{
    public static IServiceCollection AddSeniorProfilePersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<SeniorProfileDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, SeniorProfileDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<SeniorProfileDbContext>>("SeniorProfile", tags: ["ready"]);
        return services;
    }
}
