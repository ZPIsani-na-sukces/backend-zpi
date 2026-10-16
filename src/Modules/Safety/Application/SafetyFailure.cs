namespace Zpi.Modules.Safety.Application;

public enum SafetyFailure
{
    ResourceNotFound,
    OperationForbidden,
    ActiveDeviceExists,
    DeviceLimitReached,
    VersionMismatch,
    ZoneLimitReached,
    MonitoringDisabled
}
