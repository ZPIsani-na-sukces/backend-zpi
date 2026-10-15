using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zpi.Database.Outbox;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Safety.Application.Notifications;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Incidents.Trigger;

public sealed class TriggerSosHandler(SafetyDbContext context, ICareFacade care, IncidentMapper mapper,
    INotificationProcessingSignal signal, TimeProvider clock)
{
    public async Task<SafetyResult<TriggeredSosDto>> Handle(TriggerSosCommand command, CancellationToken cancellationToken)
    {
        if (command.AccountId != command.SeniorId || !await care.CanAccessSeniorAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<TriggeredSosDto>.Failed(SafetyFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var request = command.Request;
        var triggeredAt = DateTimeOffset.FromUnixTimeMilliseconds(request.TriggeredAt.ToUnixTimeMilliseconds());
        var device = await context.Devices
            .SingleOrDefaultAsync(row => row.Id == request.DeviceId &&
            row.AssignedAccountId == command.SeniorId, cancellationToken);
        if (device is null) return SafetyResult<TriggeredSosDto>.Failed(SafetyFailure.ResourceNotFound);
        var receipt = await context.SosTriggerReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.DeviceId == request.DeviceId && row.TriggeredAt == triggeredAt, cancellationToken);
        if (receipt is not null)
        {
            var original = await context.SosIncidents
                .AsNoTracking()
                .SingleAsync(row => row.Id == receipt.IncidentId, cancellationToken);
            return SafetyResult<TriggeredSosDto>.Success(new TriggeredSosDto((await mapper.MapAsync([original], cancellationToken))[0], false));
        }
        if (device.RetiredAt is not null) return SafetyResult<TriggeredSosDto>.Failed(SafetyFailure.ResourceNotFound);
        var incident = await context.SosIncidents
            .SingleOrDefaultAsync(row => row.SeniorId == command.SeniorId &&
            row.Status != IncidentStatus.Resolved, cancellationToken);
        var created = incident is null;
        var now = clock.GetUtcNow();
        if (incident is null)
        {
            var position = request.Position;
            if (position is null)
            {
                var devices = context.Devices
                    .Where(row => row.AssignedAccountId == command.SeniorId)
                    .Select(row => row.Id);
                position = await context.LocationSamples
                    .AsNoTracking()
                    .Where(row => devices.Contains(row.DeviceId) && row.CapturedAt <= triggeredAt)
                    .OrderByDescending(row => row.CapturedAt)
                    .ThenByDescending(row => row.ReceivedAt)
                    .ThenByDescending(row => row.Id)
                    .Select(row => new IncidentPositionDto(new(row.Latitude, row.Longitude), row.AccuracyMeters, row.SpeedMps, row.CapturedAt))
                    .FirstOrDefaultAsync(cancellationToken);
            }
            incident = new SosIncident
            {
                SeniorId = command.SeniorId,
                DeviceId = device.Id,
                Status = IncidentStatus.Open,
                OccurredAt = triggeredAt,
                ReceivedAt = now,
                Latitude = position?.Coordinates.Latitude,
                Longitude = position?.Coordinates.Longitude,
                AccuracyMeters = position?.AccuracyMeters,
                SpeedMps = position?.SpeedMps,
                PositionCapturedAt = position is null ? null : DateTimeOffset.FromUnixTimeMilliseconds(position.CapturedAt.ToUnixTimeMilliseconds())
            };
            context.SosIncidents.Add(incident);
            var recipients = await care.GetActiveCaretakerIdsAsync(command.SeniorId, cancellationToken);
            foreach (var recipient in recipients)
            {
                var eventId = Guid.CreateVersion7();
                var message = new NotificationEvent(eventId, recipient, command.SeniorId, NotificationType.Sos,
                    "Wezwanie pomocy SOS", "Senior potrzebuje pomocy. Otwórz aplikację, aby zobaczyć zgłoszenie.",
                    NotificationTargetKind.Incident, incident.Id, null);
                context.OutboxMessages.Add(new OutboxMessage
                {
                    Id = eventId,
                    Type = SafetyNotificationEvents.SosTriggered,
                    Payload = JsonSerializer.Serialize(message),
                    OccurredAt = now
                });
            }
        }
        context.SosTriggerReceipts.Add(new SosTriggerReceipt { DeviceId = device.Id, TriggeredAt = triggeredAt, IncidentId = incident.Id });
        device.LastOnlineAt = device.LastOnlineAt > now ? device.LastOnlineAt : now;
        context.Entry(device).Property(row => row.LastOnlineAt).IsModified = true;
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (created) signal.RequestProcessing();
        return SafetyResult<TriggeredSosDto>.Success(new TriggeredSosDto((await mapper.MapAsync([incident], cancellationToken))[0], created));
    }
}
