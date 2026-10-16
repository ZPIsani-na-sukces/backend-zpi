using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Notifications;

public sealed class SafetyNotificationSource(SafetyDbContext context, TimeProvider clock) : INotificationEventSource
{
    public string Name => SafetyNotificationEvents.Source;

    public Task PrepareAsync(CancellationToken cancellationToken) => context.OutboxMessages
        .Where(row => SafetyNotificationEvents.Types.Contains(row.Type) && row.ProcessedAt < clock.GetUtcNow().AddDays(-7))
        .ExecuteDeleteAsync(cancellationToken);

    public async Task<IReadOnlyList<PendingNotificationEvent>> ReadPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await context.OutboxMessages
            .AsNoTracking()
            .Where(row => SafetyNotificationEvents.Types.Contains(row.Type) && row.ProcessedAt == null && row.Attempts < 10 &&
                (row.NextAttemptAt == null || row.NextAttemptAt <= now))
            .OrderBy(row => row.OccurredAt)
            .ThenBy(row => row.Id)
            .Take(limit)
            .Select(row => new PendingNotificationEvent(row.Id, row.ContractVersion, row.Payload))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsRelevantAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        if (notification.TargetKind != NotificationTargetKind.Incident || notification.SeniorId is null ||
            notification.RecipientAccountId == notification.SeniorId) return false;
        var seniorId = notification.SeniorId.Value;
        if (notification.Type == NotificationType.Sos)
            return await context.SosIncidents
            .AsNoTracking()
            .AnyAsync(row => row.Id == notification.TargetId &&
                row.SeniorId == seniorId && row.Status == IncidentStatus.Open, cancellationToken);
        var state = await context.AutomaticAlertStates
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.SeniorId == seniorId, cancellationToken);
        if (state is null || !await context.Devices.AnyAsync(row => row.Id == state.DeviceId && row.MonitoringEnabled && row.RetiredAt == null, cancellationToken)) return false;
        if (notification.Type == NotificationType.GeofenceExit && state.GeofenceNotificationActive && state.GeofenceIncidentId == notification.TargetId)
            return await context.GeofenceIncidents
            .AsNoTracking()
            .AnyAsync(row => row.Id == notification.TargetId &&
                row.SeniorId == seniorId && row.Status == IncidentStatus.Open, cancellationToken);
        if (notification.Type == NotificationType.LowBattery && state.BatteryNotificationActive && state.BatteryIncidentId == notification.TargetId)
            return await context.LowBatteryIncidents
            .AsNoTracking()
            .AnyAsync(row => row.Id == notification.TargetId &&
                row.SeniorId == seniorId && row.Status == IncidentStatus.Open, cancellationToken);
        return false;
    }

    public Task CompleteAsync(Guid eventId, CancellationToken cancellationToken) => context.OutboxMessages
        .Where(row => row.Id == eventId && SafetyNotificationEvents.Types.Contains(row.Type) && row.ProcessedAt == null)
        .ExecuteUpdateAsync(update => update.SetProperty(row => row.ProcessedAt, clock.GetUtcNow())
            .SetProperty(row => row.NextAttemptAt, (DateTimeOffset?)null).SetProperty(row => row.LastError, (string?)null), cancellationToken);

    public Task RetryAsync(Guid eventId, CancellationToken cancellationToken) => context.OutboxMessages
        .Where(row => row.Id == eventId && SafetyNotificationEvents.Types.Contains(row.Type) && row.ProcessedAt == null)
        .ExecuteUpdateAsync(update => update.SetProperty(row => row.Attempts, row => row.Attempts + 1)
            .SetProperty(row => row.NextAttemptAt, clock.GetUtcNow().AddMinutes(1))
            .SetProperty(row => row.LastError, INotificationEventSource.ProcessingFailureCode), cancellationToken);
}
