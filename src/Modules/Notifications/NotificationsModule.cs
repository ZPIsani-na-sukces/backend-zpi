using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Database;
using Zpi.Modules.Notifications.Persistence;

namespace Zpi.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<NotificationsDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, NotificationsDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<NotificationsDbContext>>("Notifications", tags: ["ready"]);
        return services;
    }
}
