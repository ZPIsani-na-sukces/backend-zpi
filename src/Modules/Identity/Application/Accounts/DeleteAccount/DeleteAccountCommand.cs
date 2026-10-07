namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed record DeleteAccountCommand(Guid AccountId, string Password);
