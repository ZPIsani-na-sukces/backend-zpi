namespace Zpi.Modules.Safety.Application.Devices.Register;

public sealed record RegisterDeviceCommand(Guid AccountId, RegisterDeviceRequest Request);
