using Zpi.Domain;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed class IncidentMapper(IIdentityFacade identity)
{
    public async Task<IReadOnlyList<IncidentDto>> MapAsync(IReadOnlyList<SosIncident> rows, CancellationToken cancellationToken)
    {
        var ids = rows.SelectMany(row => new[] { row.AcknowledgedByAccountId, row.ResolvedByAccountId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var accounts = await identity.GetAccountSummariesAsync(ids, cancellationToken);
        return rows
            .Select(row => new IncidentDto(row.Id, row.SeniorId, row.DeviceId, IncidentType.Sos.ToCamelCase(),
            row.Status.ToCamelCase(), row.OccurredAt, row.ReceivedAt,
            row.Latitude is { } latitude && row.Longitude is { } longitude && row.PositionCapturedAt is { } captured
                ? new IncidentPositionDto(new CoordinatesDto(latitude, longitude), row.AccuracyMeters, row.SpeedMps, captured) : null,
            new IncidentDetailsDto(IncidentType.Sos.ToCamelCase()), row.AcknowledgedAt,
            row.AcknowledgedByAccountId is { } acknowledged ? accounts.GetValueOrDefault(acknowledged) : null,
            row.ResolvedAt, row.ResolvedByAccountId is { } resolved ? accounts.GetValueOrDefault(resolved) : null, row.ResolutionNote)).ToArray();
    }
}
