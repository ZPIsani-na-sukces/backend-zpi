namespace Zpi.Modules.Identity.Application.Authentication.PhoneVerification;

public sealed record ConfirmPhoneVerificationCommand(Guid ChallengeId, string Code);
