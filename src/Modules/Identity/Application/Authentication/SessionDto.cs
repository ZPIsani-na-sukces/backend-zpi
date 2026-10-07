using Zpi.Modules.Identity.Application.Accounts;

namespace Zpi.Modules.Identity.Application.Authentication;

public sealed record SessionDto(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string? RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt, AccountDto User);
