using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Shared.Pagination;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Infrastructure;

internal sealed class IdentityFacade(IdentityDbContext context) : IIdentityFacade
{
    public async Task<IReadOnlyDictionary<Guid, AccountSummary>> GetAccountSummariesAsync(IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(user => accountIds.Contains(user.Id))
            .Join(context.Roles, user => user.RoleId, role => role.Id,
                (user, role) => new { user.Id, user.FirstName, user.LastName, role.Code })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(account => account.Id,
            account => new AccountSummary(account.Id, account.FirstName, account.LastName, account.Code.ToCamelCase()));
    }

    public async Task<AccountSummary?> FindActiveSeniorAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalized = identifier.Trim();
        var email = normalized.ToUpperInvariant();
        var account = await context.Accounts
            .AsNoTracking()
            .Where(user => user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null &&
                (user.NormalizedPhoneNumber == normalized || user.NormalizedEmail == email))
            .Join(context.Roles.Where(role => role.Code == AccountRole.Senior), user => user.RoleId, role => role.Id,
                (user, role) => new { user.Id, user.FirstName, user.LastName, role.Code })
            .SingleOrDefaultAsync(cancellationToken);
        return account is null ? null : new AccountSummary(account.Id, account.FirstName, account.LastName, account.Code.ToCamelCase());
    }

    public async Task<IReadOnlyDictionary<Guid, AccountSummary>> GetActiveAccountsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(user => accountIds.Contains(user.Id) && user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null)
            .Join(context.Roles, user => user.RoleId, role => role.Id,
                (user, role) => new { user.Id, user.FirstName, user.LastName, role.Code })
            .ToListAsync(cancellationToken);
        return accounts.ToDictionary(account => account.Id,
            account => new AccountSummary(account.Id, account.FirstName, account.LastName, account.Code.ToCamelCase()));
    }

    public async Task<PageDto<AccountSummary>> GetActiveAccountPageAsync(IReadOnlyCollection<Guid> accountIds,
        string? search, int page, int limit, CancellationToken cancellationToken)
    {
        var rows = context.Accounts
            .AsNoTracking()
            .Where(user => accountIds.Contains(user.Id) && user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null)
            .Join(context.Roles, user => user.RoleId, role => role.Id,
                (user, role) => new { user.Id, user.FirstName, user.LastName, role.Code });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            rows = rows.Where(user => user.FirstName.ToUpper().Contains(term) || user.LastName.ToUpper().Contains(term));
        }
        var total = await rows.CountAsync(cancellationToken);
        var accounts = await rows.OrderBy(user => user.LastName).ThenBy(user => user.FirstName).ThenBy(user => user.Id)
            .Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return new PageDto<AccountSummary>(accounts.Select(account => new AccountSummary(account.Id,
            account.FirstName, account.LastName, account.Code.ToCamelCase())).ToList(), page, limit, total);
    }
}
