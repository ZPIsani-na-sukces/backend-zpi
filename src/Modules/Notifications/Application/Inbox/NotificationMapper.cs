using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Inbox;

public sealed class NotificationMapper(NotificationsDbContext context)
{
    public async Task<IReadOnlyList<NotificationDto>> MapAsync(IReadOnlyList<Notification> rows, CancellationToken cancellationToken)
    {
        var ids = rows
            .Select(row => row.Id).ToArray();
        var deliveries = await context.NotificationDeliveries
            .AsNoTracking()
            .Where(row => ids.Contains(row.NotificationId))
            .OrderBy(row => row.Channel)
            .ToListAsync(cancellationToken);
        var lookup = deliveries.ToLookup(row => row.NotificationId);
        return rows
            .Select(row => new NotificationDto(row.Id, row.SeniorId, row.Type.ToCamelCase(), row.Title, row.Message,
            row.CreatedAt, row.ReadAt, new NotificationTargetDto(row.TargetKind.ToCamelCase(),
                row.TargetKind == NotificationTargetKind.Incident ? row.TargetId : null,
                row.TargetKind == NotificationTargetKind.TaskOccurrence ? row.TargetId : null,
                row.TargetKind == NotificationTargetKind.CareInvitation ? row.TargetId : null),
            lookup[row.Id].Select(delivery => new NotificationDeliveryDto(delivery.Channel.ToCamelCase(), delivery.Status.ToCamelCase(),
                delivery.SentAt, delivery.DeliveredAt)).ToArray())).ToArray();
    }
}
