using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed class UpdatePhoneNumberHandler(IdentityDbContext context, VerificationChallengeVerifier challenges, TimeProvider clock)
{
    public async Task<AccountResult<AccountDto>> Handle(UpdatePhoneNumberCommand command, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == command.AccountId &&
            row.Status == AccountStatus.Active && row.PhoneVerifiedAt != null, cancellationToken);
        if (account is null) return AccountResult<AccountDto>.Failed(AccountFailure.ResourceNotFound);
        if (account.Version != command.ExpectedVersion)
            return AccountResult<AccountDto>.Failed(AccountFailure.VersionMismatch);

        var now = clock.GetUtcNow();
        var challenge = await challenges.FindAsync(command.ChallengeId, ChallengePurpose.PhoneVerification, now, cancellationToken);
        if (challenge is null || challenge.AccountId != account.Id || challenge.Channel != ChallengeChannel.Sms || challenge.TicketId is not null)
            return AccountResult<AccountDto>.Failed(AccountFailure.InvalidCode);

        if (!await challenges.VerifyCodeAsync(challenge, account, command.Code, now, cancellationToken))
            return AccountResult<AccountDto>.Failed(AccountFailure.InvalidCode);

        var phone = challenge.Destination;
        if (await context.Accounts.AnyAsync(row => row.Id != command.AccountId &&
            row.NormalizedPhoneNumber == phone, cancellationToken))
            return AccountResult<AccountDto>.Failed(AccountFailure.DuplicateAccount);

        challenge.ConsumedAt = now;
        account.PhoneNumber = phone;
        account.NormalizedPhoneNumber = phone;
        account.PhoneVerifiedAt = now;
        account.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);

        var role = await context.Roles.AsNoTracking().SingleAsync(row => row.Id == account.RoleId, cancellationToken);
        return AccountResult<AccountDto>.Success(AccountDtoMapper.Map(account, role.Code));
    }
}
