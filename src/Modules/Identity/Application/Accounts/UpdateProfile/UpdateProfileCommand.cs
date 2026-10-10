namespace Zpi.Modules.Identity.Application.Accounts.UpdateProfile;

public sealed record UpdateProfileCommand(Guid AccountId, string FirstName, string LastName, int ExpectedVersion);
