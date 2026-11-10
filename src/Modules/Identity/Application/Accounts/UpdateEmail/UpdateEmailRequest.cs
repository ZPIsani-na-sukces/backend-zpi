namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed record UpdateEmailRequest(Guid ChallengeId, string Code);
