using Zpi.Domain;

namespace Zpi.Modules.Safety.Domain;

public sealed class AutomaticAlertState : VersionedEntity
{
    public Guid SeniorId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTimeOffset? LocationEvaluatedAt { get; set; }
    public bool? IsInside { get; set; }
    public bool? CandidateInside { get; set; }
    public DateTimeOffset? CandidateSince { get; set; }
    public string InsideZonesJson { get; set; } = "[]";
    public Guid? GeofenceIncidentId { get; set; }
    public DateTimeOffset? GeofenceEnteredAt { get; set; }
    public bool GeofenceNotificationActive { get; set; }
    public DateTimeOffset? BatteryEvaluatedAt { get; set; }
    public bool BatteryArmed { get; set; } = true;
    public Guid? BatteryIncidentId { get; set; }
    public bool BatteryNotificationActive { get; set; }

    public void ResetGeofence()
    {
        GeofenceEnteredAt = null;
        IsInside = null;
        CandidateInside = null;
        CandidateSince = null;
        InsideZonesJson = "[]";
        GeofenceNotificationActive = false;
    }
}
