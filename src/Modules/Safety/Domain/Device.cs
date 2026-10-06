using Zpi.Domain;

namespace Zpi.Modules.Safety.Domain;

public sealed class Device : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid AssignedAccountId { get; set; }
    public Guid InstallationId { get; set; }
    public DevicePlatform Platform { get; set; }
    public required string AppVersion { get; set; }
    public bool MonitoringEnabled { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset? LastOnlineAt { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
}
