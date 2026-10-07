namespace Zpi.Modules.Identity.Application.Accounts.ChangePassword;

public sealed record ChangePasswordCommand(Guid AccountId, ChangePasswordRequest Passwords);
