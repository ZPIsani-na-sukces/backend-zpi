using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.Safety.Persistence;

internal sealed class AccountDataCleanup(SafetyDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var devices = context.Devices.Where(row => row.AssignedAccountId == accountId).Select(row => row.Id);
        await context.SosIncidents.Where(row => row.SeniorId == accountId || devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        await context.GeofenceIncidents.Where(row => row.SeniorId == accountId || devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        await context.LowBatteryIncidents.Where(row => row.SeniorId == accountId || devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        await context.LocationSamples.Where(row => devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        await context.BatterySamples.Where(row => devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        await context.Devices.Where(row => row.AssignedAccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.GeoZones.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.HomeAddresses.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
