using Zpi.Shared.Pagination;
using Zpi.Modules.Notifications.Domain;

namespace Zpi.Modules.Notifications.Application.Inbox.List;

public sealed class ListNotificationsRequest : PageRequest
{
    public Guid? SeniorId { get; init; }
    public NotificationType? Type { get; init; }
    public bool? IsRead { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
}
