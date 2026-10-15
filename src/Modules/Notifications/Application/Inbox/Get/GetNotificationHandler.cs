using Microsoft.EntityFrameworkCore;

namespace Zpi.Modules.Notifications.Application.Inbox.Get;

public sealed class GetNotificationHandler(NotificationAccess access, NotificationMapper mapper)
{
    public async Task<NotificationResult<NotificationDto>> Handle(GetNotificationQuery query, CancellationToken cancellationToken)
    {
        var row = await (await access.VisibleAsync(query.AccountId, cancellationToken))
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == query.NotificationId, cancellationToken);
        return row is null ? NotificationResult<NotificationDto>.Failed(NotificationFailure.ResourceNotFound) :
            NotificationResult<NotificationDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
