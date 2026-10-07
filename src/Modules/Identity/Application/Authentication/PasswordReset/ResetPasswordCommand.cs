namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed record ResetPasswordCommand(Guid ChallengeId, string Code, string NewPassword);
