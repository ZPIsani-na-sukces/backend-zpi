namespace Zpi.Modules.Safety.Domain;

public sealed class SosTriggerReceipt
{
    public Guid DeviceId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; }
    public Guid IncidentId { get; set; }
}
