namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed record UpdateEmailCommand(Guid AccountId, Guid ChallengeId, string Code, int ExpectedVersion);
