using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Routines.Api;
using Zpi.Modules.Routines.Application;
using Zpi.Modules.Routines.Infrastructure.BackgroundJobs;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines;

public static class RoutinesModule
{
    public static IServiceCollection AddRoutinesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRoutinesApi();
        services.AddRoutinesApplication();
        services.AddRoutinesPersistence(configuration);
        if (configuration.GetValue("Routines:WorkerEnabled", true))
            services.AddHostedService<OccurrenceWorker>();
        return services;
    }
}
