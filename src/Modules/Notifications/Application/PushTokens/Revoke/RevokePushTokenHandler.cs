using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.PushTokens.Revoke;

public sealed class RevokePushTokenHandler(NotificationsDbContext context, TimeProvider clock)
{
    public async Task<NotificationResult<bool>> Handle(RevokePushTokenCommand command, CancellationToken cancellationToken)
    {
        var rows = context.PushDeviceTokens
            .Where(row => row.Id == command.RegistrationId &&
            row.AccountId == command.AccountId && row.SessionId == command.SessionId);
        if (!await rows
            .AnyAsync(cancellationToken)) return NotificationResult<bool>.Failed(NotificationFailure.ResourceNotFound);
        await rows
            .Where(row => row.RevokedAt == null).ExecuteUpdateAsync(update =>
            update.SetProperty(row => row.RevokedAt, clock.GetUtcNow()), cancellationToken);
        return NotificationResult<bool>.Success(true);
    }
}
