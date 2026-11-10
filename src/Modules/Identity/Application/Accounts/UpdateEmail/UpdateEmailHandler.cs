using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed class UpdateEmailHandler(IdentityDbContext context, VerificationChallengeVerifier challenges, TimeProvider clock)
{
    public async Task<AccountResult<AccountDto>> Handle(UpdateEmailCommand command, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == command.AccountId &&
            row.Status == AccountStatus.Active && row.PhoneVerifiedAt != null, cancellationToken);
        if (account is null) return AccountResult<AccountDto>.Failed(AccountFailure.ResourceNotFound);
        if (account.Version != command.ExpectedVersion)
            return AccountResult<AccountDto>.Failed(AccountFailure.VersionMismatch);

        var now = clock.GetUtcNow();
        var challenge = await challenges.FindAsync(command.ChallengeId, ChallengePurpose.EmailVerification, now, cancellationToken);
        if (challenge is null || challenge.AccountId != account.Id || challenge.Channel != ChallengeChannel.Email)
            return AccountResult<AccountDto>.Failed(AccountFailure.InvalidCode);

        if (!await challenges.VerifyCodeAsync(challenge, account, command.Code, now, cancellationToken))
            return AccountResult<AccountDto>.Failed(AccountFailure.InvalidCode);

        var email = challenge.Destination.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (await context.Accounts.AnyAsync(row => row.Id != command.AccountId &&
            row.NormalizedEmail == normalizedEmail, cancellationToken))
            return AccountResult<AccountDto>.Failed(AccountFailure.DuplicateAccount);

        challenge.ConsumedAt = now;
        account.Email = email;
        account.NormalizedEmail = normalizedEmail;
        account.EmailVerifiedAt = now;
        account.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        var role = await context.Roles.AsNoTracking().SingleAsync(row => row.Id == account.RoleId, cancellationToken);
        return AccountResult<AccountDto>.Success(AccountDtoMapper.Map(account, role.Code));
    }
}
