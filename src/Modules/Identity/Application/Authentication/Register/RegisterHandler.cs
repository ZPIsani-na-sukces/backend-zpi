using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.Register;

public sealed class RegisterHandler(IdentityDbContext context, PasswordHashing passwords, IPhoneCodeSender sender,
    IPhoneVerificationCodes codes, TimeProvider clock)
{
    public async Task<AuthenticationResult<RegistrationDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (!sender.IsAvailable)
            return AuthenticationResult<RegistrationDto>.Failed(AuthenticationFailure.DeliveryUnavailable);
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
        var (challenge, code) = VerificationChallengeFactory.Create(account, ChallengePurpose.PhoneVerification,
            ChallengeChannel.Sms, phone, passwords, now, ticket, codes.FixedCodeFor(phone));
        context.VerificationChallenges.Add(challenge);
        
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        
        await context.SaveChangesAsync(cancellationToken);
        
        var delivery = await sender.SendAsync(challenge.Id, phone, code, cancellationToken);
        if (delivery != PhoneCodeDelivery.Sent)
        {
            await transaction.RollbackAsync(cancellationToken);
            return AuthenticationResult<RegistrationDto>.Failed(delivery == PhoneCodeDelivery.InvalidPhoneNumber
                ? AuthenticationFailure.InvalidPhoneNumber : AuthenticationFailure.DeliveryUnavailable);
        }
        
        await transaction.CommitAsync(CancellationToken.None);

        return AuthenticationResult<RegistrationDto>.Success(new RegistrationDto(account.Id, "pendingPhoneVerification",
            secret, ticket.ExpiresAt, new ChallengeDto(challenge.Id, "sms", challenge.ExpiresAt, challenge.ResendAfter)));
    }
}
