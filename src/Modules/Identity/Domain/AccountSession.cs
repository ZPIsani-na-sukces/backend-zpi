using Zpi.Domain;

namespace Zpi.Modules.Identity.Domain;

public sealed class AccountSession : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AccountId { get; set; }
    public ClientType ClientType { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
