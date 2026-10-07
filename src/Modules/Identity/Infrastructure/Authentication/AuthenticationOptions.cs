using System.ComponentModel.DataAnnotations;

namespace Zpi.Modules.Identity.Infrastructure.Authentication;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";
    [Required] public string Issuer { get; set; } = "zpi-backend";
    [Required] public string Audience { get; set; } = "zpi-app";
    [Required, MinLength(64)] public string SigningKey { get; set; } = "";
    public string[] AllowedWebOrigins { get; set; } = [];
}
