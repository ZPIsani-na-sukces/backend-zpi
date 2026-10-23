using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PhoneVerification;

public sealed class ConfirmPhoneVerificationHandler(IdentityDbContext context, VerificationChallengeVerifier challenges, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ConfirmPhoneVerificationCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await challenges.FindAsync(command.ChallengeId, ChallengePurpose.PhoneVerification, now, cancellationToken);
        if (challenge is null || challenge.Channel != ChallengeChannel.Sms || challenge.TicketId is null)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var ticket = await context.VerificationTickets.SingleOrDefaultAsync(row => row.Id == challenge.TicketId, cancellationToken);
        if (ticket is null || ticket.AccountId != challenge.AccountId || ticket.ConsumedAt is not null || ticket.ExpiresAt <= now)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.PendingPhoneVerification || account.PhoneVerifiedAt is not null)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidState);

        if (!await challenges.VerifyCodeAsync(challenge, account, command.Code, now, cancellationToken))
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);

        challenge.ConsumedAt = now;
        ticket.ConsumedAt = now;
        account.Status = AccountStatus.Active;
        account.PhoneVerifiedAt = now;
        account.UpdatedAt = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        }
        return AuthenticationResult<bool>.Success(true);
    }
}
