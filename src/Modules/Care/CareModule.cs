using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Care.Api;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Infrastructure.BackgroundJobs;
using Zpi.Modules.Care.Application;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Care.Infrastructure;
using Zpi.Modules.Care.Infrastructure.Persistence;

namespace Zpi.Modules.Care;

public static class CareModule
{
    public static IServiceCollection AddCareModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICareFacade, CareFacade>();
        services.AddSingleton<IInvitationCodeGenerator, InvitationCodeGenerator>();
        if (configuration.GetValue("Care:CleanupWorkerEnabled", true))
            services.AddHostedService<InvitationCleanupWorker>();
        services.AddCareApplication();
        services.AddCareApi();
        services.AddCarePersistence(configuration);
        return services;
    }
}
