using Zpi.Modules.Identity.Application.Sessions;

namespace Zpi.Modules.Identity.Application.Authentication.Logout;

public sealed class LogoutHandler(SessionRevoker revoker)
{
    public Task Handle(LogoutCommand command, CancellationToken cancellationToken) => revoker.RevokeSessionAsync(command.SessionId, cancellationToken);
}
