namespace Zpi.Domain;

public interface ISessionCleanup
{
    Task ForSessionAsync(Guid sessionId, CancellationToken cancellationToken);
    Task ForAccountAsync(Guid accountId, CancellationToken cancellationToken);
}
