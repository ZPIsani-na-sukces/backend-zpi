namespace Zpi.Modules.Notifications.Domain;

public sealed class PushDeviceToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AccountId { get; set; }
    public Guid SessionId { get; set; }
    public Guid InstallationId { get; set; }
    public PushPlatform Platform { get; set; }
    public required string TokenHash { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
