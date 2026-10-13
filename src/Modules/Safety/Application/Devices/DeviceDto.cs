using Zpi.Domain;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Devices;

public sealed record DeviceDto(Guid DeviceId, string Platform, string AppVersion, bool MonitoringEnabled,
    DateTimeOffset RegisteredAt, DateTimeOffset? LastOnlineAt)
{
    public static DeviceDto FromDevice(Device device) => new(device.Id, device.Platform.ToCamelCase(),
        device.AppVersion, device.MonitoringEnabled, device.RegisteredAt, device.LastOnlineAt);
}
