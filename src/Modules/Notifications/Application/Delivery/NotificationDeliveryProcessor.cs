using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed class NotificationDeliveryProcessor(NotificationsDbContext context, IIdentityFacade identity, ICareFacade care,
    PushNotificationDispatcher push, ISmsNotificationSender sms, TimeProvider clock, ILogger<NotificationDeliveryProcessor> logger)
{
    public async Task ProcessAsync(IReadOnlyDictionary<string, INotificationEventSource> sources, CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var now = clock.GetUtcNow();
        var ids = await context.NotificationDeliveries
            .AsNoTracking()
            .Where(row => row.Status == DeliveryStatus.Pending && (row.NextAttemptAt == null || row.NextAttemptAt <= now))
            .OrderBy(row => row.NextAttemptAt)
            .ThenBy(row => row.Id)
            .Select(row => row.Id)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        foreach (var id in ids)
        {
            context.ChangeTracker.Clear();
            try { await DeliverAsync(id, sources, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (DbUpdateConcurrencyException) { context.ChangeTracker.Clear(); }
            catch (Exception exception) { logger.LogError(exception, "Notification delivery {DeliveryId} failed. Its lease will expire.", id); }
        }
    }

    private async Task DeliverAsync(Guid id, IReadOnlyDictionary<string, INotificationEventSource> sources, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var delivery = await context.NotificationDeliveries
            .SingleOrDefaultAsync(row => row.Id == id && row.Status == DeliveryStatus.Pending &&
            (row.NextAttemptAt == null || row.NextAttemptAt <= now), cancellationToken);
        if (delivery is null) return;
        delivery.NextAttemptAt = now.AddMinutes(2);
        delivery.AttemptCount++;
        await context.SaveChangesAsync(cancellationToken);
        var notification = await context.Notifications
            .AsNoTracking()
            .SingleAsync(row => row.Id == delivery.NotificationId, cancellationToken);
        var recipient = await identity.GetNotificationRecipientAsync(notification.RecipientAccountId, cancellationToken);
        var message = new NotificationEvent(notification.IntegrationEventId, notification.RecipientAccountId, notification.SeniorId,
            notification.Type, notification.Title, notification.Message, notification.TargetKind, notification.TargetId, notification.ExpiresAt);
        if (recipient is null || notification.ExpiresAt <= now ||
            notification.SeniorId is { } seniorId && !await care.CanAccessSeniorAsync(notification.RecipientAccountId, seniorId, cancellationToken) ||
            !sources.TryGetValue(notification.Source, out var source) || !await source.IsRelevantAsync(message, cancellationToken))
        {
            delivery.Status = DeliveryStatus.Cancelled;
            delivery.NextAttemptAt = null;
            await context.SaveChangesAsync(cancellationToken);
            return;
        }
        DeliverySendResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            result = delivery.Channel == DeliveryChannel.Sms
                ? await sms.SendAsync(delivery.Id.ToString("N"), recipient.PhoneNumber, notification.Message, timeout.Token)
                : await push.SendAsync(delivery, notification, timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogError(exception, "Notification provider failed for {DeliveryId}.", delivery.Id);
            result = new DeliverySendResult(false, ErrorCode: "providerUnavailable");
        }
        if (result.Accepted)
        {
            delivery.Status = DeliveryStatus.Sent;
            delivery.SentAt = clock.GetUtcNow();
            delivery.ProviderMessageId = result.ProviderMessageId;
            delivery.LastErrorCode = null;
            delivery.NextAttemptAt = null;
        }
        else
        {
            delivery.LastErrorCode = result.ErrorCode ?? "providerRejected";
            delivery.Status = result.Retryable && delivery.AttemptCount < 5 ? DeliveryStatus.Pending : DeliveryStatus.Failed;
            delivery.NextAttemptAt = delivery.Status == DeliveryStatus.Pending
                ? clock.GetUtcNow().AddSeconds(Math.Min(300, 15 * Math.Pow(2, delivery.AttemptCount - 1))) : null;
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
