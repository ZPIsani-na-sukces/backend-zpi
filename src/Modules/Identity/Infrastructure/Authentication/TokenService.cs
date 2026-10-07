using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Zpi.Domain;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Authentication;

public sealed class TokenService(IOptions<AuthenticationOptions> options)
{
    public static string CreateSecret() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
    public static string HashSecret(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public string CreateAccessToken(Account account, AccountRole role, Guid sessionId, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        var settings = options.Value;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim("sub", account.Id.ToString()),
                new Claim("sid", sessionId.ToString()),
                new Claim("role", role.ToCamelCase()),
                new Claim("jti", Guid.CreateVersion7().ToString())]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256)
        });
    }
}
