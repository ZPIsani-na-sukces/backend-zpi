using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Shared.Pagination;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Infrastructure;

internal sealed class IdentityFacade(IdentityDbContext context) : IIdentityFacade
{
    public async Task<AccountSummary?> FindActiveSeniorAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalized = identifier.Trim();
        var email = normalized.ToUpperInvariant();
        var account = await (from user in context.Accounts.AsNoTracking()
            join role in context.Roles on user.RoleId equals role.Id
            where user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null && role.Code == AccountRole.Senior &&
                (user.NormalizedPhoneNumber == normalized || user.NormalizedEmail == email)
            select new { user.Id, user.FirstName, user.LastName, role.Code }).SingleOrDefaultAsync(cancellationToken);
        return account is null ? null : new AccountSummary(account.Id, account.FirstName, account.LastName, account.Code.ToCamelCase());
    }

    public async Task<IReadOnlyDictionary<Guid, AccountSummary>> GetActiveAccountsAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        var accounts = await (from user in context.Accounts.AsNoTracking()
            join role in context.Roles on user.RoleId equals role.Id
            where accountIds.Contains(user.Id) && user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null
            select new { user.Id, user.FirstName, user.LastName, role.Code }).ToListAsync(cancellationToken);
        return accounts.ToDictionary(account => account.Id,
            account => new AccountSummary(account.Id, account.FirstName, account.LastName, account.Code.ToCamelCase()));
    }
    public async Task<PageDto<AccountSummary>> GetActiveAccountPageAsync(IReadOnlyCollection<Guid> accountIds,
        string? search, int page, int limit, CancellationToken cancellationToken)
    {
        var rows = from user in context.Accounts.AsNoTracking()
            join role in context.Roles on user.RoleId equals role.Id
            where accountIds.Contains(user.Id) && user.Status == AccountStatus.Active && user.PhoneVerifiedAt != null
            select new { user.Id, user.FirstName, user.LastName, role.Code };
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
