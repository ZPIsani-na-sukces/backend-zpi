using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Sessions;

public interface ISessionStore
{
    Task SaveAsync(AccountSession session, CancellationToken cancellationToken);
    Task<AccountSession?> GetAsync(Guid sessionId, CancellationToken cancellationToken);
    Task<AccountSession?> FindByRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<bool> RotateAsync(AccountSession session, string newTokenHash, CancellationToken cancellationToken);
    Task RevokeAsync(Guid sessionId, CancellationToken cancellationToken);
    Task RevokeAllAsync(Guid accountId, CancellationToken cancellationToken);
}
