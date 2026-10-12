using Zpi.Shared.Pagination;

namespace Zpi.Modules.Identity.Contracts;

public interface IIdentityFacade
{
    Task<IReadOnlyDictionary<Guid, AccountSummary>> GetAccountSummariesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
    Task<AccountSummary?> FindActiveSeniorAsync(string identifier, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, AccountSummary>> GetActiveAccountsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
    Task<PageDto<AccountSummary>> GetActiveAccountPageAsync(IReadOnlyCollection<Guid> accountIds, string? search,
        int page, int limit, CancellationToken cancellationToken);
}
