using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Notifications.Application.Delivery;
using Zpi.Modules.Notifications.Application.Inbox;
using Zpi.Modules.Notifications.Application.Inbox.Get;
using Zpi.Modules.Notifications.Application.Inbox.List;
using Zpi.Modules.Notifications.Application.Inbox.Read;
using Zpi.Modules.Notifications.Application.Outbox;
using Zpi.Modules.Notifications.Contracts;

namespace Zpi.Modules.Notifications.Application;

public static class NotificationsApplicationSetup
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        services.AddScoped<NotificationAccess>();
        services.AddScoped<NotificationMapper>();
        services.AddScoped<ListNotificationsHandler>();
        services.AddScoped<GetNotificationHandler>();
        services.AddScoped<ReadNotificationHandler>();
        services.AddScoped<INotificationsFacade, NotificationsFacade>();
        services.AddScoped<NotificationProcessor>();
        services.AddScoped<NotificationOutboxProcessor>();
        services.AddScoped<NotificationDeliveryProcessor>();
        services.AddValidatorsFromAssemblyContaining<NotificationEventValidator>();
        return services;
    }
}
