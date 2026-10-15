using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed class NotificationProcessor(NotificationsDbContext context, IEnumerable<INotificationEventSource> eventSources,
    INotificationsFacade notifications, IIdentityFacade identity, ICareFacade care,
    IPushNotificationSender push, ISmsNotificationSender sms, TimeProvider clock, ILogger<NotificationProcessor> logger)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var sources = eventSources.ToDictionary(source => source.Name);
        foreach (var source in sources.Values)
        {
            try
            {
                await source.PrepareAsync(cancellationToken);
                var pending = await source.ReadPendingAsync(100, cancellationToken);
                foreach (var row in pending)
                {
                    context.ChangeTracker.Clear();
                    try
                    {
                        if (row.ContractVersion != 1) throw new InvalidOperationException("Unsupported notification event version.");
                        var message = JsonSerializer.Deserialize<NotificationEvent>(row.Payload)
                            ?? throw new JsonException("Missing notification event.");
                        if (message.EventId != row.Id) throw new JsonException("Invalid notification event id.");
                        if (await source.IsRelevantAsync(message, cancellationToken))
                            await notifications.EnqueueAsync(source.Name, message, cancellationToken);
                        await source.CompleteAsync(row.Id, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        context.ChangeTracker.Clear();
                        logger.LogError(exception, "Notification event {EventId} from {Source} failed.", row.Id, source.Name);
                        await source.RetryAsync(row.Id, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError(exception, "Notification source {Source} failed.", source.Name); }
        }
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
                : await SendPushAsync(delivery, notification, timeout.Token);
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

    private async Task<DeliverySendResult> SendPushAsync(NotificationDelivery delivery, Notification notification, CancellationToken cancellationToken)
    {
        var tokens = await context.PushDeviceTokens
            .AsNoTracking()
            .Where(row => row.AccountId == notification.RecipientAccountId && row.RevokedAt == null)
            .OrderBy(row => row.Id)
            .ToListAsync(cancellationToken);
        var results = new List<DeliverySendResult>();
        foreach (var token in tokens)
        {
            if (!await identity.IsSessionActiveAsync(token.AccountId, token.SessionId, cancellationToken))
            { await RevokeTokenAsync(token, cancellationToken); continue; }
            var result = await push.SendAsync($"{delivery.Id:N}:{token.Id:N}", token.Token,
                notification.Title, notification.Message, notification.Id, cancellationToken);
            if (result.InvalidToken) await RevokeTokenAsync(token, cancellationToken);
            results.Add(result);
        }
        var retry = results.FirstOrDefault(result => !result.Accepted && result.Retryable && !result.InvalidToken);
        return retry ?? results.FirstOrDefault(result => result.Accepted) ??
            new DeliverySendResult(false, ErrorCode: "noPushDevices");
    }

    private Task RevokeTokenAsync(PushDeviceToken token, CancellationToken cancellationToken) => context.PushDeviceTokens
        .Where(row => row.Id == token.Id && row.SessionId == token.SessionId && row.UpdatedAt == token.UpdatedAt && row.RevokedAt == null)
        .ExecuteUpdateAsync(update => update.SetProperty(row => row.RevokedAt, clock.GetUtcNow()), cancellationToken);

}
