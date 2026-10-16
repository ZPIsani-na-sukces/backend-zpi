using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed class IncidentReader(SafetyDbContext context)
{
    public IQueryable<IncidentRow> Query(Guid seniorId)
    {
        var sos = context.SosIncidents
            .AsNoTracking()
            .Where(row => row.SeniorId == seniorId)
            .Select(row => new IncidentRow
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
                ZoneId = (Guid?)null,
                ZoneName = (string?)null,
                ZonesJson = (string?)null,
                BatteryLevelPercent = (int?)null
            });
        var geofence = context.GeofenceIncidents
            .AsNoTracking()
            .Where(row => row.SeniorId == seniorId)
            .Select(row => new IncidentRow
            {
                Id = row.Id,
                SeniorId = row.SeniorId,
                DeviceId = row.DeviceId,
                Type = IncidentType.GeofenceExit,
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
                ZoneId = row.GeoZoneId,
                ZoneName = row.ZoneName,
                ZonesJson = row.ZonesJson,
                BatteryLevelPercent = (int?)null
            });
        var lowbattery = context.LowBatteryIncidents
            .AsNoTracking()
            .Where(row => row.SeniorId == seniorId)
            .Select(row => new IncidentRow
            {
                Id = row.Id,
                SeniorId = row.SeniorId,
                DeviceId = row.DeviceId,
                Type = IncidentType.LowBattery,
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
                ZoneId = (Guid?)null,
                ZoneName = (string?)null,
                ZonesJson = (string?)null,
                BatteryLevelPercent = row.BatteryLevelPercent
            });
        return sos.Concat(geofence).Concat(lowbattery);
    }

    public Task<IncidentRow?> FindAsync(Guid seniorId, Guid incidentId, CancellationToken cancellationToken) => Query(seniorId)
        .SingleOrDefaultAsync(row => row.Id == incidentId, cancellationToken);
}
