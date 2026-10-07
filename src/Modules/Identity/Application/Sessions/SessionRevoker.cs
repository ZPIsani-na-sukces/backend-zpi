using Zpi.Domain;

namespace Zpi.Modules.Identity.Application.Sessions;

public sealed class SessionRevoker(ISessionStore sessions, IEnumerable<ISessionCleanup> cleanupHandlers)
{
    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(sessionId, cancellationToken);
        foreach (var cleanup in cleanupHandlers)
            await cleanup.ForSessionAsync(sessionId, cancellationToken);
    }

    public async Task RevokeAccountAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await sessions.RevokeAllAsync(accountId, cancellationToken);
        foreach (var cleanup in cleanupHandlers)
            await cleanup.ForAccountAsync(accountId, cancellationToken);
    }
}
