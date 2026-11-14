using Zpi.Shared.Pagination;

namespace Zpi.Modules.Identity.Contracts;

public interface IIdentityFacade
{
    Task<NotificationRecipient?> GetNotificationRecipientAsync(Guid accountId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, AccountSummary>> GetAccountSummariesAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, AccountSummary>> GetActiveAccountsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken);
    Task<CursorPageDto<AccountSummary>> GetActiveAccountCursorPageAsync(IReadOnlyCollection<Guid> accountIds, string? search,
        string? cursor, int limit, string scope, CancellationToken cancellationToken);
}
