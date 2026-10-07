namespace Zpi.Modules.Identity.Domain;

public sealed class AccountSession
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AccountId { get; set; }
    public Guid SecurityStamp { get; set; }
    public ClientType ClientType { get; set; }
    public required string RefreshTokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
