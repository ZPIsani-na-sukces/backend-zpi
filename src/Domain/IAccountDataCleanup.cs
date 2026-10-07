namespace Zpi.Domain;

public interface IAccountDataCleanup
{
    Task DeleteAsync(Guid accountId, CancellationToken cancellationToken);
}
