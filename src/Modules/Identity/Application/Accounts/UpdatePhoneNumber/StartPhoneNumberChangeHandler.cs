using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed class StartPhoneNumberChangeHandler(IdentityDbContext context, PasswordHashing passwords,
    IPhoneCodeSender sender, IPhoneVerificationCodes codes, TimeProvider clock)
{
    public async Task<AuthenticationResult<ChallengeDto>> Handle(StartPhoneNumberChangeCommand command,
        CancellationToken cancellationToken)
    {
        if (!sender.IsAvailable)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);

        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == command.AccountId &&
            row.Status == AccountStatus.Active && row.PhoneVerifiedAt != null, cancellationToken);
        if (account is null)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.OperationForbidden);

        var phone = command.PhoneNumber.Trim();
        if (account.NormalizedPhoneNumber == phone)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);

        if (await context.Accounts.AnyAsync(row => row.Id != command.AccountId &&
            row.NormalizedPhoneNumber == phone, cancellationToken))
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DuplicateAccount);

        var now = clock.GetUtcNow();
        var previous = await context.VerificationChallenges
            .Where(row => row.AccountId == account.Id && row.Purpose == ChallengePurpose.PhoneVerification &&
                row.TicketId == null)
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken);

        if (previous.Count > 0 && previous[0].ResendAfter > now)
            return AuthenticationResult<ChallengeDto>.CoolingDown(previous[0].ResendAfter - now);

        foreach (var item in previous.Where(row => row.ConsumedAt is null && row.InvalidatedAt is null))
            item.InvalidatedAt = now;

        var (challenge, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.PhoneVerification,
            ChallengeChannel.Sms, phone, passwords, now, null, codes.FixedCodeFor(phone));
        context.VerificationChallenges.Add(challenge);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        }

        var delivery = await sender.SendAsync(challenge.Id, phone, code, cancellationToken);
        if (delivery != PhoneCodeDelivery.Sent)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthenticationResult<ChallengeDto>.Failed(delivery == PhoneCodeDelivery.InvalidPhoneNumber
                ? AuthenticationFailure.InvalidPhoneNumber
                : AuthenticationFailure.DeliveryUnavailable);
        }

        await transaction.CommitAsync(CancellationToken.None);
        return AuthenticationResult<ChallengeDto>.Success(new ChallengeDto(challenge.Id, "sms", challenge.ExpiresAt, challenge.ResendAfter));
    }
}
