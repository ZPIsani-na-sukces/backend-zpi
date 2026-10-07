using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed class RegisterHandler(IdentityDbContext context, PasswordHashing passwords, TimeProvider clock)
{
    public async Task<AuthenticationResult<RegistrationDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();
        var email = request.Email?.Trim();
        var normalizedEmail = email?.ToUpperInvariant();
        if (await context.Accounts.AnyAsync(account => account.NormalizedPhoneNumber == phone ||
            (normalizedEmail != null && account.NormalizedEmail == normalizedEmail), cancellationToken))
            return AuthenticationResult<RegistrationDto>.Failed(AuthenticationFailure.DuplicateAccount);

        var now = clock.GetUtcNow();
        var role = await context.Roles.SingleAsync(role => role.Code == request.Role, cancellationToken);
        var account = new Account
        {
            RoleId = role.Id,
            Email = email,
            NormalizedEmail = normalizedEmail,
            PhoneNumber = phone,
            NormalizedPhoneNumber = phone,
            PasswordHash = "",
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Status = AccountStatus.PendingPhoneVerification,
            CreatedAt = now,
            UpdatedAt = now
        };
        account.PasswordHash = passwords.Hash(account, request.Password);
        context.Accounts.Add(account);
        var (ticket, secret) = VerificationTickets.Create(context, account.Id, now);
        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var challenge = new VerificationChallenge
        {
            AccountId = account.Id,
            TicketId = ticket.Id,
            Purpose = ChallengePurpose.PhoneVerification,
            Channel = ChallengeChannel.Sms,
            Destination = phone,
            SecretHash = passwords.Hash(account, code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5),
            ResendAfter = now.AddSeconds(60)
        };
        context.VerificationChallenges.Add(challenge);
        await context.SaveChangesAsync(cancellationToken);
        return AuthenticationResult<RegistrationDto>.Success(new RegistrationDto(account.Id, "pendingPhoneVerification",
            secret, ticket.ExpiresAt, new ChallengeDto(challenge.Id, "sms", challenge.ExpiresAt, challenge.ResendAfter)));
    }
}
