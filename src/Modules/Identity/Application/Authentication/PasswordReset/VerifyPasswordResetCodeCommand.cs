namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed record VerifyPasswordResetCodeCommand(Guid ChallengeId, string Code);
