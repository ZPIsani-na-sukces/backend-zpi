using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.EmailVerification;

public sealed class StartEmailVerificationHandler(IdentityDbContext context, PasswordHashing passwords,
    IEmailCodeSender sender, TimeProvider clock)
{
    public async Task<AuthenticationResult<ChallengeDto>> Handle(StartEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        if (!sender.IsAvailable)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);
        var account = await context.Accounts.SingleAsync(row => row.Id == command.AccountId, cancellationToken);
        if (account.Status != AccountStatus.Active || account.Email is null || account.EmailVerifiedAt is not null)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        var now = clock.GetUtcNow();
        var previous = await context.VerificationChallenges.Where(row => row.AccountId == account.Id &&
                row.Purpose == ChallengePurpose.EmailVerification)
            .OrderByDescending(row => row.CreatedAt).ToListAsync(cancellationToken);
        if (previous.Count > 0 && previous[0].ResendAfter > now)
            return AuthenticationResult<ChallengeDto>.CoolingDown(previous[0].ResendAfter - now);
        foreach (var challenge in previous.Where(row => row.ConsumedAt is null && row.InvalidatedAt is null))
            challenge.InvalidatedAt = now;
        var (next, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.EmailVerification,
            ChallengeChannel.Email, account.Email, passwords, now);
        context.VerificationChallenges.Add(next);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        }
        await sender.SendAsync(next.Id, account.Email, code, cancellationToken);
        return AuthenticationResult<ChallengeDto>.Success(new ChallengeDto(next.Id, "email", next.ExpiresAt, next.ResendAfter));
    }
}
