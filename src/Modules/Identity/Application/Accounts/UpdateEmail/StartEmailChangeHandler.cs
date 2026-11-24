using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed class StartEmailChangeHandler(IdentityDbContext context, PasswordHashing passwords,
    IEmailCodeSender sender, TimeProvider clock)
{
    public async Task<AuthenticationResult<ChallengeDto>> Handle(StartEmailChangeCommand command,
        CancellationToken cancellationToken)
    {
        if (!sender.IsAvailable)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);

        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == command.AccountId &&
            row.Status == AccountStatus.Active && row.PhoneVerifiedAt != null, cancellationToken);
        if (account is null)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.OperationForbidden);

        var email = command.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();

        if (account.NormalizedEmail == normalizedEmail)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);

        if (await context.Accounts.AnyAsync(row => row.Id != command.AccountId &&
            row.NormalizedEmail == normalizedEmail, cancellationToken))
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DuplicateAccount);

        var now = clock.GetUtcNow();
        var previous = await context.VerificationChallenges
            .Where(row => row.AccountId == account.Id && row.Purpose == ChallengePurpose.EmailVerification)
            .OrderByDescending(row => row.CreatedAt)
            .ToListAsync(cancellationToken);

        if (previous.Count > 0 && previous[0].ResendAfter > now)
            return AuthenticationResult<ChallengeDto>.CoolingDown(previous[0].ResendAfter - now);

        foreach (var item in previous.Where(row => row.ConsumedAt is null && row.InvalidatedAt is null))
            item.InvalidatedAt = now;

        var (challenge, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.EmailVerification,
            ChallengeChannel.Email, email, passwords, now);
        context.VerificationChallenges.Add(challenge);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        }

        try
        {
            await sender.SendAsync(new EmailCodeDelivery(challenge.Id, email, code, challenge.Purpose, challenge.ExpiresAt), cancellationToken);
        }
        catch (EmailDeliveryException)
        {
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);
        }
        return AuthenticationResult<ChallengeDto>.Success(new ChallengeDto(challenge.Id, "email", challenge.ExpiresAt, challenge.ResendAfter));
    }
}
