using Microsoft.EntityFrameworkCore;
using FluentValidation;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Delivery;

internal sealed class NotificationsFacade(NotificationsDbContext context, IIdentityFacade identity, TimeProvider clock, IValidator<NotificationEvent> validator) : INotificationsFacade
{
    public async Task EnqueueAsync(string source, NotificationEvent message, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(message, cancellationToken);
        if (await context.Notifications
            .AnyAsync(row => row.IntegrationEventId == message.EventId &&
            row.RecipientAccountId == message.RecipientAccountId, cancellationToken)) return;
        var recipient = await identity.GetNotificationRecipientAsync(message.RecipientAccountId, cancellationToken);
        if (recipient is null || message.ExpiresAt <= clock.GetUtcNow()) return;
        var now = clock.GetUtcNow();
        var notification = new Notification
        {
            Source = source,
            IntegrationEventId = message.EventId,
            RecipientAccountId = recipient.UserId,
            SeniorId = message.SeniorId,
            Type = message.Type,
            Title = message.Title,
            Message = message.Message,
            TargetKind = message.TargetKind,
            TargetId = message.TargetId,
            CreatedAt = now,
            ExpiresAt = message.ExpiresAt
        };
        context.Notifications.Add(notification);
        context.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationId = notification.Id,
            Channel = DeliveryChannel.InApp,
            Status = DeliveryStatus.Delivered,
            SentAt = now,
            DeliveredAt = now
        });
        context.NotificationDeliveries.Add(new NotificationDelivery
        {
            NotificationId = notification.Id,
            Channel = recipient.Role == "senior" ? DeliveryChannel.Push : DeliveryChannel.Sms,
            Status = DeliveryStatus.Pending,
            NextAttemptAt = now
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
