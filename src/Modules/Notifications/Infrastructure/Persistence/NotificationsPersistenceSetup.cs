using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Domain;

namespace Zpi.Modules.Notifications.Infrastructure.Persistence;

public static class NotificationsPersistenceSetup
{
    public static IServiceCollection AddNotificationsPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        services.AddScoped<ISessionCleanup, SessionCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<NotificationsDbContext>(options => DatabaseConfiguration.Configure(options, connectionString, NotificationsDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<NotificationsDbContext>>("Notifications", tags: ["ready"]);
        return services;
    }
}
