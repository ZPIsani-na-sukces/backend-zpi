namespace Zpi.Modules.Identity.Application.Authentication.Refresh;

public sealed record RefreshCommand(RefreshRequest Request, string? CookieToken);
