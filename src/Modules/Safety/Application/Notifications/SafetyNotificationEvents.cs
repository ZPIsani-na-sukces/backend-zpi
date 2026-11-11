namespace Zpi.Modules.Safety.Application.Notifications;

public static class SafetyNotificationEvents
{
    public const string Source = "safety";
    public const string GeofenceEntered = "geofenceEntered";
    public const string GeofenceExited = "geofenceExited";
    public const string LowBatteryDetected = "lowBatteryDetected";
    public static readonly string[] Types = [SosTriggered, GeofenceExited, GeofenceEntered, LowBatteryDetected];
    public const string SosTriggered = "sosTriggered";
}
