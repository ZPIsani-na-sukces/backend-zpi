using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Devices;

internal static class DeviceReader
{
    public static async Task<DeviceDto[]> ReadAsync(SafetyDbContext context, IQueryable<Device> devices,
        CancellationToken cancellationToken)
    {
        var rows = await devices.Select(device => new
        {
            Device = device,
            Battery = context.BatterySamples.Where(sample => sample.DeviceId == device.Id)
                .OrderByDescending(sample => sample.CapturedAt)
                .ThenByDescending(sample => sample.ReceivedAt)
                .ThenByDescending(sample => sample.Id)
                .Select(sample => new DeviceBatteryStatusDto(sample.BatteryLevelPercent, sample.IsCharging, sample.CapturedAt))
                .FirstOrDefault()
        }).ToArrayAsync(cancellationToken);
        return rows.Select(row => DeviceDto.FromDevice(row.Device) with { LatestBattery = row.Battery }).ToArray();
    }

    public static async Task<DeviceDto> ReadAsync(SafetyDbContext context, Device device,
        CancellationToken cancellationToken) =>
        (await ReadAsync(context, context.Devices.AsNoTracking().Where(row => row.Id == device.Id), cancellationToken)).Single();
}
