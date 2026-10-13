namespace Zpi.Modules.Safety.Application.Devices.Retire;

public sealed record RetireDeviceCommand(Guid AccountId, Guid DeviceId);
