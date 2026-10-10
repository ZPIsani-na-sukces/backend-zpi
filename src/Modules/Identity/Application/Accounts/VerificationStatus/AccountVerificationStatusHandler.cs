using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.VerificationStatus;

public sealed class AccountVerificationStatusHandler(IdentityDbContext context)
{
    public Task<AccountVerificationStatusDto> Handle(AccountVerificationStatusQuery query, CancellationToken cancellationToken) =>
        context.Accounts.AsNoTracking().Where(account => account.Id == query.AccountId)
            .Select(account => new AccountVerificationStatusDto(account.PhoneVerifiedAt != null, account.EmailVerifiedAt != null))
            .SingleAsync(cancellationToken);
}
