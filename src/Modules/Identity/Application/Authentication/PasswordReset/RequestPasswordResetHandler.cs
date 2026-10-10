using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication.Register;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class RequestPasswordResetHandler(IdentityDbContext context, PasswordHashing passwords,
    IPhoneCodeSender phoneSender, IEmailCodeSender emailSender, TimeProvider clock)
{
    public async Task<AuthenticationResult<ChallengeDto>> Handle(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        if (command.Channel == ChallengeChannel.Sms ? !phoneSender.IsAvailable : !emailSender.IsAvailable)
            return AuthenticationResult<ChallengeDto>.Failed(AuthenticationFailure.DeliveryUnavailable);
        var now = clock.GetUtcNow();
        var identifier = command.Identifier.Trim();
        var account = command.Channel == ChallengeChannel.Sms
            ? await context.Accounts.SingleOrDefaultAsync(row => row.NormalizedPhoneNumber == identifier, cancellationToken)
            : await context.Accounts.SingleOrDefaultAsync(row => row.NormalizedEmail == identifier.ToUpperInvariant(), cancellationToken);
        if (account is null || account.Status != AccountStatus.Active ||
            (command.Channel == ChallengeChannel.Sms && account.PhoneVerifiedAt is null))
            return AuthenticationResult<ChallengeDto>.Success(PublicResponse(command.Channel, now));

        var previous = await context.VerificationChallenges.Where(row => row.AccountId == account.Id &&
                row.Purpose == ChallengePurpose.PasswordReset)
            .OrderByDescending(row => row.CreatedAt).ToListAsync(cancellationToken);
        if (previous.Count > 0 && previous[0].ResendAfter > now)
            return AuthenticationResult<ChallengeDto>.Success(PublicResponse(command.Channel, now));
        foreach (var challenge in previous.Where(row => row.ConsumedAt is null && row.InvalidatedAt is null))
            challenge.InvalidatedAt = now;
        var destination = command.Channel == ChallengeChannel.Sms ? account.PhoneNumber : account.Email!;
        var (next, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.PasswordReset,
            command.Channel, destination, passwords, now);
        context.VerificationChallenges.Add(next);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<ChallengeDto>.Success(PublicResponse(command.Channel, now));
        }
        if (command.Channel == ChallengeChannel.Sms)
            await phoneSender.SendAsync(next.Id, destination, code, cancellationToken);
        else
            await emailSender.SendAsync(next.Id, destination, code, cancellationToken);
        return AuthenticationResult<ChallengeDto>.Success(new ChallengeDto(next.Id, command.Channel == ChallengeChannel.Sms ? "sms" : "email",
            next.ExpiresAt, next.ResendAfter));
    }

    private static ChallengeDto PublicResponse(ChallengeChannel channel, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), channel == ChallengeChannel.Sms ? "sms" : "email", now.AddMinutes(5), now.AddSeconds(60));
}
