using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords;

public static class MedicalRecordsModule
{
    public static IServiceCollection AddMedicalRecordsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<MedicalRecordsDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, MedicalRecordsDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<MedicalRecordsDbContext>>("MedicalRecords", tags: ["ready"]);
        return services;
    }
}
