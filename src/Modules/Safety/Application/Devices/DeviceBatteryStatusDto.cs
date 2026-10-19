namespace Zpi.Modules.Safety.Application.Devices;

public sealed record DeviceBatteryStatusDto(int LevelPercent, bool IsCharging, DateTimeOffset CapturedAt);
