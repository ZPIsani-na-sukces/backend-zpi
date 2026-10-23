using Microsoft.AspNetCore.Http;

namespace Zpi.Modules.Identity.Api;

internal static class RefreshTokenCookie
{
    public const string Name = "zpi_refresh_token";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static void Clear(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Cookies.Delete(Name, Options());
    }

    private static CookieOptions Options(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1/auth",
        Expires = expires
    };
}
