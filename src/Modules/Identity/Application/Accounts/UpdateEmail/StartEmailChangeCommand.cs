namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed record StartEmailChangeCommand(Guid AccountId, string Email);
