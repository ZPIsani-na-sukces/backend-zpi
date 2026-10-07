namespace Zpi.Modules.Identity.Application.Accounts.ChangePassword;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
