namespace Zpi.Modules.Identity.Application.Authentication.EmailVerification;

public sealed record ConfirmEmailVerificationCommand(Guid ChallengeId, string Code);
