using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Delivery;

public sealed class PushNotificationDispatcher(NotificationsDbContext context, IIdentityFacade identity,
    IPushNotificationSender push, TimeProvider clock)
{
    public async Task<DeliverySendResult> SendAsync(NotificationDelivery delivery, Notification notification, CancellationToken cancellationToken)
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
