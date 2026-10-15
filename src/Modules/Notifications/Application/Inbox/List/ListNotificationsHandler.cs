using Microsoft.EntityFrameworkCore;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Notifications.Application.Inbox.List;

public sealed class ListNotificationsHandler(NotificationAccess access, NotificationMapper mapper)
{
    public async Task<PageDto<NotificationDto>> Handle(ListNotificationsQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;
        var rows = (await access.VisibleAsync(query.AccountId, cancellationToken))
            .AsNoTracking();
        if (request.SeniorId.HasValue) rows = rows
            .Where(row => row.SeniorId == request.SeniorId);
        if (request.Type.HasValue) rows = rows
            .Where(row => row.Type == request.Type);
        if (request.IsRead.HasValue) rows = rows
            .Where(row => (row.ReadAt != null) == request.IsRead);
        if (request.From.HasValue)
        {
            var from = request.From.Value.ToUniversalTime(); rows = rows
            .Where(row => row.CreatedAt >= from);
        }
        if (request.To.HasValue)
        {
            var to = request.To.Value.ToUniversalTime(); rows = rows
            .Where(row => row.CreatedAt < to);
        }
        var count = await rows
            .CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);
        return new PageDto<NotificationDto>(await mapper.MapAsync(items, cancellationToken), request.Page, request.Limit, count);
    }
}
