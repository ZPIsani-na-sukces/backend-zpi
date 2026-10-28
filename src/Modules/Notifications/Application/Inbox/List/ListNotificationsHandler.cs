using Microsoft.EntityFrameworkCore;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Notifications.Application.Inbox.List;

public sealed class ListNotificationsHandler(NotificationAccess access, NotificationMapper mapper)
{
    public async Task<CursorPageDto<NotificationDto>> Handle(ListNotificationsQuery query, CancellationToken cancellationToken)
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
        var pagination = rows.Paginate(request.Cursor, new { query.AccountId, request.SeniorId, request.Type,
            request.IsRead, request.From, request.To }).OrderBy(row => row.CreatedAt, true).OrderBy(row => row.Id, true);
        var items = await pagination.Take(request.Limit).ToListAsync(cancellationToken);
        return new CursorPageDto<NotificationDto>(await mapper.MapAsync(items.Take(request.Limit).ToList(), cancellationToken),
            pagination.NextCursor(items, request.Limit));
    }
}
