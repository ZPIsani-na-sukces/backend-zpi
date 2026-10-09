namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class VerifyPasswordResetCodeHandler(PasswordResetCodeVerifier codes)
{
    public async Task<AuthenticationResult<bool>> Handle(VerifyPasswordResetCodeCommand command,
        CancellationToken cancellationToken)
    {
        var verified = await codes.VerifyAsync(command.ChallengeId, command.Code, cancellationToken);
        return verified is null
            ? AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode)
            : AuthenticationResult<bool>.Success(true);
    }
}
