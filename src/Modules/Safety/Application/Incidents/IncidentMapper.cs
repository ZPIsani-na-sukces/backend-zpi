using System.Text.Json;
using Zpi.Modules.Safety.Application.Alerts;
using Zpi.Domain;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed class IncidentMapper(IIdentityFacade identity)
{
    public Task<IReadOnlyList<IncidentDto>> MapAsync(IReadOnlyList<SosIncident> rows, CancellationToken cancellationToken) =>
        MapAsync(rows.Select(row => new IncidentRow
        {
            Id = row.Id,
            SeniorId = row.SeniorId,
            DeviceId = row.DeviceId,
            Type = IncidentType.Sos,
            Status = row.Status,
            OccurredAt = row.OccurredAt,
            ReceivedAt = row.ReceivedAt,
            Latitude = row.Latitude,
            Longitude = row.Longitude,
            AccuracyMeters = row.AccuracyMeters,
            SpeedMps = row.SpeedMps,
            PositionCapturedAt = row.PositionCapturedAt,
            AcknowledgedAt = row.AcknowledgedAt,
            AcknowledgedByAccountId = row.AcknowledgedByAccountId,
            ResolvedAt = row.ResolvedAt,
            ResolvedByAccountId = row.ResolvedByAccountId,
            ResolutionNote = row.ResolutionNote,
            ZoneId = null,
            ZoneName = null,
            ZonesJson = null,
            BatteryLevelPercent = null
        }).ToArray(), cancellationToken);

    public async Task<IReadOnlyList<IncidentDto>> MapAsync(IReadOnlyList<IncidentRow> rows, CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(row => new[] { row.AcknowledgedByAccountId, row.ResolvedByAccountId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var accounts = await identity.GetAccountSummariesAsync(ids, cancellationToken);
        return rows
            .Select(row => new IncidentDto(row.Id, row.SeniorId, row.DeviceId, row.Type.ToCamelCase(),
            row.Status.ToCamelCase(), row.OccurredAt, row.ReceivedAt,
            row.Latitude is { } latitude && row.Longitude is { } longitude && row.PositionCapturedAt is { } captured
                ? new IncidentPositionDto(new CoordinatesDto(latitude, longitude), row.AccuracyMeters, row.SpeedMps, captured) : null,
            new IncidentDetailsDto(row.Type.ToCamelCase())
            {
                ZoneId = row.ZoneId,
                ZoneName = row.ZoneName,
                Zones = row.ZonesJson is null ? null : JsonSerializer.Deserialize<DepartedZoneDto[]>(row.ZonesJson),
                BatteryLevelPercent = row.BatteryLevelPercent
            }, row.AcknowledgedAt,
            row.AcknowledgedByAccountId is { } acknowledged ? accounts.GetValueOrDefault(acknowledged) : null,
            row.ResolvedAt, row.ResolvedByAccountId is { } resolved ? accounts.GetValueOrDefault(resolved) : null, row.ResolutionNote)).ToArray();
    }
}
