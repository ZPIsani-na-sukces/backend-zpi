using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Accounts;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.CurrentAccount;

public sealed class CurrentAccountHandler(IdentityDbContext context)
{
    public async Task<AccountDto> Handle(CurrentAccountQuery query, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.AsNoTracking().SingleAsync(account => account.Id == query.AccountId, cancellationToken);
        var role = await context.Roles.AsNoTracking().SingleAsync(role => role.Id == account.RoleId, cancellationToken);
        return AccountDtoMapper.Map(account, role.Code);
    }
}
