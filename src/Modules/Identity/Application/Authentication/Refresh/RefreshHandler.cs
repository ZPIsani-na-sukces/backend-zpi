using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.Refresh;

public sealed class RefreshHandler(IdentityDbContext context, TokenService tokens, ISessionStore sessions,
    SessionRevoker revoker, TimeProvider clock)
{
    public async Task<AuthenticationResult<SessionDto>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var token = request.ClientType == ClientType.Web ? command.CookieToken : request.RefreshToken;
        if (string.IsNullOrWhiteSpace(token))
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        var hash = TokenService.HashSecret(token);
        var session = await sessions.FindByRefreshTokenAsync(hash, cancellationToken);
        if (session is null || session.ExpiresAt <= clock.GetUtcNow() || session.ClientType != request.ClientType)
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        if (session.RefreshTokenHash != hash)
        {
            await revoker.RevokeSessionAsync(session.Id, cancellationToken);
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        }
        var account = await context.Accounts.SingleOrDefaultAsync(account => account.Id == session.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active || account.PhoneVerifiedAt is null || account.SecurityStamp != session.SecurityStamp)
        {
            await revoker.RevokeSessionAsync(session.Id, cancellationToken);
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        }
        var role = await context.Roles.SingleAsync(role => role.Id == account.RoleId, cancellationToken);
        var refresh = TokenService.CreateSecret();
        if (!await sessions.RotateAsync(session, TokenService.HashSecret(refresh), cancellationToken))
        {
            await revoker.RevokeSessionAsync(session.Id, cancellationToken);
            return AuthenticationResult<SessionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        }
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(15);
        return AuthenticationResult<SessionDto>.Success(new SessionDto(
            tokens.CreateAccessToken(account, role.Code, session.Id, now, expires), expires, refresh, session.ExpiresAt,
            AccountDtoMapper.Map(account, role.Code)));
    }
}
