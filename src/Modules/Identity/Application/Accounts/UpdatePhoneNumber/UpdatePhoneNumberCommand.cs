namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed record UpdatePhoneNumberCommand(Guid AccountId, Guid ChallengeId, string Code, int ExpectedVersion);
