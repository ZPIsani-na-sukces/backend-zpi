using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Zpi.Database.Outbox;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Alerts;

public sealed class AutomaticAlertProcessor(SafetyDbContext context, ICareFacade care, IIdentityFacade identity,
    IOptions<AlertOptions> options)
{
    public async Task<bool> EvaluateAsync(Device device, IReadOnlyList<LocationSample> locations, IReadOnlyList<BatterySample> batteries,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var state = await context.AutomaticAlertStates.SingleOrDefaultAsync(row => row.SeniorId == device.AssignedAccountId, cancellationToken);
        if (state is null)
        {
            state = new AutomaticAlertState { SeniorId = device.AssignedAccountId, DeviceId = device.Id };
            context.AutomaticAlertStates.Add(state);
        }
        if (state.DeviceId != device.Id)
        {
            state.DeviceId = device.Id;
            state.ResetGeofence();
            state.LocationEvaluatedAt = null;
            state.BatteryEvaluatedAt = null;
            state.BatteryArmed = true;
            state.BatteryNotificationActive = false;
        }
        var zones = await context.GeoZones
            .AsNoTracking()
            .Where(row => row.SeniorId == state.SeniorId && row.DeletedAt == null && row.IsActive)
            .ToListAsync(cancellationToken);
        var geoActive = await context.GeofenceIncidents.AnyAsync(row => row.SeniorId == state.SeniorId && row.Status != IncidentStatus.Resolved, cancellationToken);
        var batteryActive = await context.LowBatteryIncidents.AnyAsync(row => row.SeniorId == state.SeniorId && row.Status != IncidentStatus.Resolved, cancellationToken);
        var queued = false;
        IReadOnlyCollection<Guid>? recipients = null;
        foreach (var sample in locations.OrderBy(row => row.CapturedAt))
        {
            if (!GeofenceDetector.Evaluate(state, sample, zones, now, options.Value) || geoActive) continue;
            var departed = JsonSerializer.Deserialize<DepartedZoneDto[]>(state.InsideZonesJson)!;
            var incident = new GeofenceIncident
            {
                SeniorId = state.SeniorId,
                DeviceId = device.Id,
                Status = IncidentStatus.Open,
                OccurredAt = sample.CapturedAt,
                ReceivedAt = now,
                Latitude = sample.Latitude,
                Longitude = sample.Longitude,
                AccuracyMeters = sample.AccuracyMeters,
                SpeedMps = sample.SpeedMps,
                PositionCapturedAt = sample.CapturedAt,
                GeoZoneId = departed[0].ZoneId,
                ZoneName = departed[0].Name,
                ZonesJson = state.InsideZonesJson
            };
            context.GeofenceIncidents.Add(incident);
            geoActive = true;
            state.GeofenceIncidentId = incident.Id;
            state.GeofenceNotificationActive = departed.Any(row => row.SmsOnExitEnabled);
            if (state.GeofenceNotificationActive)
            {
                recipients ??= await care.GetActiveCaretakerIdsAsync(state.SeniorId, cancellationToken);
                queued = true;
                var senior = await SeniorDisplayName.LoadAsync(identity, state.SeniorId, cancellationToken);
                Enqueue(recipients, state.SeniorId, incident.Id, SafetyNotificationEvents.GeofenceExited, NotificationType.GeofenceExit,
                    "Opuszczenie bezpiecznego obszaru", $"{senior} opuścił bezpieczne geostrefy", now);
            }
        }
        foreach (var sample in batteries.OrderBy(row => row.CapturedAt))
        {
            if (!BatteryDetector.Evaluate(state, sample, now, options.Value) || batteryActive) continue;
            var devices = context.Devices.Where(row => row.AssignedAccountId == state.SeniorId).Select(row => row.Id);
            var position = await context.LocationSamples
            .AsNoTracking()
                .Where(row => devices.Contains(row.DeviceId) && row.CapturedAt <= sample.CapturedAt)
                .OrderByDescending(row => row.CapturedAt).ThenByDescending(row => row.ReceivedAt).ThenByDescending(row => row.Id)
                .FirstOrDefaultAsync(cancellationToken);
            var uploaded = locations.Where(row => row.CapturedAt <= sample.CapturedAt)
                .OrderByDescending(row => row.CapturedAt).FirstOrDefault();
            if (uploaded is not null && (position is null || uploaded.CapturedAt >= position.CapturedAt)) position = uploaded;
            var incident = new LowBatteryIncident
            {
                SeniorId = state.SeniorId,
                DeviceId = device.Id,
                Status = IncidentStatus.Open,
                OccurredAt = sample.CapturedAt,
                ReceivedAt = now,
                BatteryLevelPercent = sample.BatteryLevelPercent,
                Latitude = position?.Latitude,
                Longitude = position?.Longitude,
                AccuracyMeters = position?.AccuracyMeters,
                SpeedMps = position?.SpeedMps,
                PositionCapturedAt = position?.CapturedAt
            };
            context.LowBatteryIncidents.Add(incident);
            batteryActive = true;
            state.BatteryIncidentId = incident.Id;
            state.BatteryNotificationActive = true;
            recipients ??= await care.GetActiveCaretakerIdsAsync(state.SeniorId, cancellationToken);
            queued = true;
            Enqueue(recipients, state.SeniorId, incident.Id, SafetyNotificationEvents.LowBatteryDetected, NotificationType.LowBattery,
                "Niski poziom baterii", "Urządzenie seniora ma niski poziom baterii. Otwórz aplikację, aby sprawdzić zgłoszenie.", now);
        }
        return queued;
    }

    private void Enqueue(IReadOnlyCollection<Guid> recipients, Guid seniorId, Guid incidentId, string eventType, NotificationType type,
        string title, string message, DateTimeOffset now)
    {
        foreach (var recipient in recipients)
        {
            var eventId = Guid.CreateVersion7();
            var notification = new NotificationEvent(eventId, recipient, seniorId, type, title, message,
                NotificationTargetKind.Incident, incidentId, null);
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = eventId,
                Type = eventType,
                OccurredAt = now,
                Payload = JsonSerializer.Serialize(notification)
            });
        }
    }
}
