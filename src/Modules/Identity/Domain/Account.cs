using Zpi.Domain;

namespace Zpi.Modules.Identity.Domain;

public sealed class Account : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RoleId { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public required string PhoneNumber { get; set; }
    public required string NormalizedPhoneNumber { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public AccountStatus Status { get; set; }
    public DateTimeOffset? PhoneVerifiedAt { get; set; }
    public DateTimeOffset? EmailVerifiedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
