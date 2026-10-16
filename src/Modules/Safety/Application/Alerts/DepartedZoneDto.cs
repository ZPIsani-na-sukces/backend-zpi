namespace Zpi.Modules.Safety.Application.Alerts;

public sealed record DepartedZoneDto(Guid ZoneId, string Name, bool SmsOnExitEnabled);
