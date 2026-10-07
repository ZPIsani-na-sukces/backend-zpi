using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Zpi.Modules.Identity.Infrastructure.BackgroundJobs;

public static class BackgroundJobsSetup
{
    public static IServiceCollection AddIdentityBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        if (configuration.GetValue("AccountDeletion:Enabled", true))
            services.AddHostedService<AccountDeletionWorker>();
        return services;
    }
}
