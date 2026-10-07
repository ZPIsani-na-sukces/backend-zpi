using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.Login;

public sealed class LoginHandler(IdentityDbContext context, PasswordHashing passwords, TokenService tokens,
    ISessionStore sessions, TimeProvider clock)
{
    public async Task<AuthenticationResult<SessionDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var email = identifier.ToUpperInvariant();
        var account = await context.Accounts.SingleOrDefaultAsync(account =>
            account.NormalizedPhoneNumber == identifier || account.NormalizedEmail == email, cancellationToken);
        var verification = passwords.Verify(account, request.Password);
        if (account is null || verification == PasswordVerificationResult.Failed)
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        if (account.Status is AccountStatus.Blocked or AccountStatus.PendingDeletion)
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.OperationForbidden);
        var now = clock.GetUtcNow();
        if (account.Status != AccountStatus.Active || account.PhoneVerifiedAt is null)
        {
            var (ticket, secret) = VerificationTickets.Create(context, account.Id, now);
            await context.SaveChangesAsync(cancellationToken);
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.PhoneVerificationRequired,
                new PhoneVerificationDto(secret, ticket.ExpiresAt));
        }
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            account.PasswordHash = passwords.Hash(account, request.Password);
        var role = await context.Roles.SingleAsync(role => role.Id == account.RoleId, cancellationToken);
        var refresh = TokenService.CreateSecret();
        var session = new AccountSession
        {
            AccountId = account.Id,
            SecurityStamp = account.SecurityStamp,
            ClientType = request.ClientType,
            RefreshTokenHash = TokenService.HashSecret(refresh),
            CreatedAt = now,
            ExpiresAt = now.AddDays(30)
        };
        var expires = now.AddMinutes(15);
        var access = tokens.CreateAccessToken(account, role.Code, session.Id, now, expires);
        await context.SaveChangesAsync(cancellationToken);
        await sessions.SaveAsync(session, cancellationToken);
        return AuthenticationResult<SessionDto>.Success(new SessionDto(access, expires, refresh, session.ExpiresAt,
            AccountDtoMapper.Map(account, role.Code)));
    }
}
