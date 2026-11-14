namespace Zpi.Modules.Identity.Application.Sessions;

public sealed class SessionRevoker(ISessionStore sessions)
{
    public Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken) =>
        sessions.RevokeAsync(sessionId, cancellationToken);

    public Task RevokeAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        sessions.RevokeAllAsync(accountId, cancellationToken);
}
