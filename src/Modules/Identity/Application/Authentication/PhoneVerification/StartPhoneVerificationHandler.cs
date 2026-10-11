using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PhoneVerification;

public sealed class StartPhoneVerificationHandler(IdentityDbContext context, PasswordHashing passwords,
    IPhoneCodeSender sender, TimeProvider clock)
{
    public async Task<AuthenticationResult<ChallengeDto>> Handle(StartPhoneVerificationCommand command, CancellationToken cancellationToken)
    {
        if (!sender.IsAvailable)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);
        var now = clock.GetUtcNow();
        var hash = TokenService.HashSecret(command.VerificationTicket);
        var ticket = await context.VerificationTickets.SingleOrDefaultAsync(row => row.TokenHash == hash, cancellationToken);
        if (ticket is null || ticket.ConsumedAt is not null || ticket.ExpiresAt <= now)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidCode);
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == ticket.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.PendingPhoneVerification)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        var previous = await context.VerificationChallenges.Where(row => row.AccountId == account.Id &&
                row.Purpose == ChallengePurpose.PhoneVerification)
            .OrderByDescending(row => row.CreatedAt).ToListAsync(cancellationToken);
        if (previous.Count > 0 && previous[0].ResendAfter > now)
            return AuthenticationResult<ChallengeDto>.CoolingDown(previous[0].ResendAfter - now);
        foreach (var challenge in previous.Where(row => row.ConsumedAt is null && row.InvalidatedAt is null))
            challenge.InvalidatedAt = now;
        var (next, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.PhoneVerification,
            ChallengeChannel.Sms, account.PhoneNumber, passwords, now, ticket);
        context.VerificationChallenges.Add(next);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.InvalidState);
        }
        await sender.SendAsync(next.Id, account.PhoneNumber, code, cancellationToken);
        return AuthenticationResult<ChallengeDto>.Success(new ChallengeDto(next.Id, "sms", next.ExpiresAt, next.ResendAfter));
    }
}
