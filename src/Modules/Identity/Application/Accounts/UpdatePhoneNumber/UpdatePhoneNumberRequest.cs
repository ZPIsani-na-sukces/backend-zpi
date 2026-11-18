namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed record UpdatePhoneNumberRequest(Guid ChallengeId, string Code);
