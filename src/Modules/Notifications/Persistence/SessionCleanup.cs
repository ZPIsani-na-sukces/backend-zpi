using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.Notifications.Persistence;

internal sealed class SessionCleanup(NotificationsDbContext context) : ISessionCleanup
{
    public Task ForSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        context.PushDeviceTokens.Where(token => token.SessionId == sessionId).ExecuteDeleteAsync(cancellationToken);

    public Task ForAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        context.PushDeviceTokens.Where(token => token.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
}
