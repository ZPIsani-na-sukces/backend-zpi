namespace Zpi.Modules.Safety.Application.Devices.List;

public sealed record ListDevicesQuery(Guid AccountId, Guid? SeniorId = null);
