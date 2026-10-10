using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UpdateProfile;

public sealed class UpdateProfileHandler(IdentityDbContext context, TimeProvider clock)
{
    public async Task<AccountResult<AccountDto>> Handle(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == command.AccountId &&
            row.Status == AccountStatus.Active && row.PhoneVerifiedAt != null, cancellationToken);
        if (account is null) return AccountResult<AccountDto>.Failed(AccountFailure.ResourceNotFound);
        if (account.Version != command.ExpectedVersion)
            return AccountResult<AccountDto>.Failed(AccountFailure.VersionMismatch);
        account.FirstName = command.FirstName.Trim();
        account.LastName = command.LastName.Trim();
        account.UpdatedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        var role = await context.Roles.AsNoTracking().SingleAsync(row => row.Id == account.RoleId, cancellationToken);
        return AccountResult<AccountDto>.Success(AccountDtoMapper.Map(account, role.Code));
    }
}
