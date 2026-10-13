using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Devices.Register;

public sealed record RegisterDeviceRequest(Guid InstallationId, DevicePlatform Platform, string AppVersion,
    bool MonitoringEnabled);
