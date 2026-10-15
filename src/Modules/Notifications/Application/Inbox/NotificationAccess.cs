using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Notifications.Infrastructure.Persistence;

namespace Zpi.Modules.Notifications.Application.Inbox;

public sealed class NotificationAccess(NotificationsDbContext context, ICareFacade care)
{
    public async Task<IQueryable<Notification>> VisibleAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var rows = context.Notifications
            .Where(row => row.RecipientAccountId == accountId);
        var seniorIds = await rows
            .Where(row => row.SeniorId != null)
            .Select(row => row.SeniorId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var accessible = await care.GetAccessibleSeniorIdsAsync(accountId, seniorIds, cancellationToken);
        return rows
            .Where(row => row.SeniorId == null || accessible.Contains(row.SeniorId.Value));
    }
}
