using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zpi.Modules.Notifications.Api;
using Zpi.Modules.Notifications.Application;
using Zpi.Modules.Notifications.Application.Delivery;
using Zpi.Modules.Notifications.Infrastructure.BackgroundJobs;
using Zpi.Modules.Notifications.Infrastructure.Delivery;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddNotificationsApi();
        services.AddNotificationsApplication();
        services.AddNotificationsPersistence(configuration);
        if (environment.IsDevelopment())
        {
            services.AddSingleton<IPushNotificationSender, DevelopmentPushNotificationSender>();
            services.AddSingleton<ISmsNotificationSender, DevelopmentSmsNotificationSender>();
        }
        else
        {
            services.AddSingleton<IPushNotificationSender, UnavailableNotificationSender>();
            services.AddSingleton<ISmsNotificationSender, UnavailableNotificationSender>();
        }
        if (configuration.GetValue("Notifications:WorkerEnabled", true)) services.AddHostedService<NotificationWorker>();
        return services;
    }
}
