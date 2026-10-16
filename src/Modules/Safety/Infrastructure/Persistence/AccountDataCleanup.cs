using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Domain;

namespace Zpi.Modules.Safety.Infrastructure.Persistence;

internal sealed class AccountDataCleanup(SafetyDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var devices = context.Devices.Where(row => row.AssignedAccountId == accountId).Select(row => row.Id);
        var incidentIds = context.SosIncidents.Where(row => row.SeniorId == accountId || devices.Contains(row.DeviceId)).Select(row => row.Id);
        await context.SosTriggerReceipts.Where(row => incidentIds.Contains(row.IncidentId) || devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
        var senior = JsonSerializer.Serialize(new { SeniorId = accountId });
        var recipient = JsonSerializer.Serialize(new { RecipientAccountId = accountId });
        await context.OutboxMessages.Where(row => SafetyNotificationEvents.Types.Contains(row.Type) &&
            (EF.Functions.JsonContains(row.Payload, senior) || EF.Functions.JsonContains(row.Payload, recipient))).ExecuteDeleteAsync(cancellationToken);
        await context.AutomaticAlertStates.Where(row => row.SeniorId == accountId || devices.Contains(row.DeviceId)).ExecuteDeleteAsync(cancellationToken);
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
