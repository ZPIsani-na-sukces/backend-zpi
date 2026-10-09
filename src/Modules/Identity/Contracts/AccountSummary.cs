namespace Zpi.Modules.Identity.Contracts;

public sealed record AccountSummary(Guid UserId, string FirstName, string LastName, string Role);
