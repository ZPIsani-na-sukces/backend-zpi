using Microsoft.EntityFrameworkCore;

namespace Zpi.Modules.Notifications.Application.Inbox.Read;

public sealed class ReadNotificationHandler(NotificationAccess access, NotificationMapper mapper, TimeProvider clock)
{
    public async Task<NotificationResult<NotificationDto>> Handle(ReadNotificationCommand command, CancellationToken cancellationToken)
    {
        var rows = (await access.VisibleAsync(command.AccountId, cancellationToken))
            .Where(row => row.Id == command.NotificationId);
        var now = clock.GetUtcNow();
        await rows
            .Where(row => row.ReadAt == null).ExecuteUpdateAsync(update =>
            update.SetProperty(row => row.ReadAt, now).SetProperty(row => row.Version, row => row.Version + 1), cancellationToken);
        var row = await rows
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? NotificationResult<NotificationDto>.Failed(NotificationFailure.ResourceNotFound) :
            NotificationResult<NotificationDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
