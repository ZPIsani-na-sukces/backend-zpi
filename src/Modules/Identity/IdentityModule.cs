using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zpi.Database;
using Zpi.Modules.Identity.Api;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Infrastructure;
using Zpi.Modules.Identity.Application;
using Zpi.Modules.Identity.Application.Accounts.DeleteAccount;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.BackgroundJobs;
using Zpi.Modules.Identity.Infrastructure.Email;
using Zpi.Modules.Identity.Infrastructure.Persistence;
using Zpi.Modules.Identity.Infrastructure.Redis;
using Zpi.Modules.Identity.Infrastructure.Sms;

namespace Zpi.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddSingleton<TimeProvider>(new PostgresTimeProvider());
        services.AddIdentitySms(configuration);
        if (environment.IsDevelopment())
            services.AddSingleton<IEmailCodeSender, DevelopmentEmailCodeSender>();
        else
            services.AddSingleton<IEmailCodeSender, UnavailableEmailCodeSender>();
        services.AddScoped<IIdentityFacade, IdentityFacade>();
        services.AddOptions<AccountDeletionOptions>()
            .Bind(configuration.GetSection(AccountDeletionOptions.SectionName))
            .Validate(options => options.GracePeriod >= TimeSpan.Zero && options.GracePeriod <= AccountDeletionOptions.MaximumGracePeriod,
                "AccountDeletion:GracePeriod must be between 00:00:00 and 365.00:00:00.")
            .ValidateOnStart();
        services.AddIdentityApplication();
        services.AddIdentityApi();
        services.AddIdentityAuthentication(configuration);
        services.AddIdentityRedis(configuration);
        services.AddIdentityRateLimiting();
        services.AddIdentityBackgroundJobs(configuration);
        services.AddIdentityPersistence(configuration);
        return services;
    }
}
