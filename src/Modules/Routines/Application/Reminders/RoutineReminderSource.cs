using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Zpi.Database.Outbox;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Contracts;
using Zpi.Modules.Notifications.Domain;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Reminders;

public sealed class RoutineReminderSource(RoutinesDbContext context, IIdentityFacade identity, TimeProvider clock) : INotificationEventSource
{
    public string Name => RoutineReminderEvent.Source;
    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        context.ChangeTracker.Clear();
        var now = clock.GetUtcNow();
        var cutoff = now.AddMinutes(-5);
        var beforeCutoff = now.AddMinutes(10);
        var soon = now.AddMinutes(15);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await context.OutboxMessages
            .Where(row => row.Type == RoutineReminderEvent.Type && row.ProcessedAt < now.AddDays(-7))
            .ExecuteDeleteAsync(cancellationToken);
        var occurrences = await context.TaskOccurrences
            .Where(row => row.Status == OccurrenceStatus.Pending &&
                (row.BeforeReminderQueuedAt == null && row.ScheduledAt > now && row.ScheduledAt >= beforeCutoff && row.ScheduledAt <= soon ||
                 row.DueReminderQueuedAt == null && row.ScheduledAt >= cutoff && row.ScheduledAt <= now))
            .Where(row => context.RoutineTasks.Any(task => task.Id == row.RoutineTaskId && task.IsActive && task.DeletedAt == null))
            .OrderBy(row => row.ScheduledAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync(occurrences
            .Select(row => row.SeniorId)
            .Distinct().ToArray(), cancellationToken);
        foreach (var row in occurrences)
        {
            var before = row.ScheduledAt > now;
            if (before) row.BeforeReminderQueuedAt = now; else row.DueReminderQueuedAt = now;
            if (!accounts.ContainsKey(row.SeniorId)) continue;
            var id = Guid.CreateVersion7();
            var message = new NotificationEvent(id, row.SeniorId, row.SeniorId, NotificationType.RoutineReminder,
                "Przypomnienie o zadaniu", before ? "Zbliża się termin zadania. Otwórz aplikację, aby zobaczyć szczegóły."
                    : "Nadszedł termin zadania. Otwórz aplikację, aby zobaczyć szczegóły.",
                NotificationTargetKind.TaskOccurrence, row.Id, before ? row.ScheduledAt : row.ScheduledAt.AddMinutes(5));
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = id,
                Type = RoutineReminderEvent.Type,
                OccurredAt = now,
                Payload = JsonSerializer.Serialize(message)
            });
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingNotificationEvent>> ReadPendingAsync(int limit, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await context.OutboxMessages
            .AsNoTracking()
            .Where(row => row.Type == RoutineReminderEvent.Type && row.ProcessedAt == null && row.Attempts < 10 &&
                (row.NextAttemptAt == null || row.NextAttemptAt <= now))
            .OrderBy(row => row.OccurredAt)
            .ThenBy(row => row.Id)
            .Take(limit)
            .Select(row => new PendingNotificationEvent(row.Id, row.ContractVersion, row.Payload))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsRelevantAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (notification.Type != NotificationType.RoutineReminder || notification.TargetKind != NotificationTargetKind.TaskOccurrence ||
            notification.RecipientAccountId != notification.SeniorId || notification.ExpiresAt <= now) return Task.FromResult(false);
        return context.TaskOccurrences
            .AsNoTracking()
            .AnyAsync(row => row.Id == notification.TargetId &&
            row.SeniorId == notification.RecipientAccountId && row.Status == OccurrenceStatus.Pending &&
            context.RoutineTasks.Any(task => task.Id == row.RoutineTaskId && task.IsActive && task.DeletedAt == null), cancellationToken);
    }

    public Task CompleteAsync(Guid eventId, CancellationToken cancellationToken) => context.OutboxMessages
        .Where(row => row.Id == eventId && row.ProcessedAt == null).ExecuteUpdateAsync(update =>
            update.SetProperty(row => row.ProcessedAt, clock.GetUtcNow()).SetProperty(row => row.NextAttemptAt, (DateTimeOffset?)null)
                .SetProperty(row => row.LastError, (string?)null), cancellationToken);

    public Task RetryAsync(Guid eventId, CancellationToken cancellationToken) => context.OutboxMessages
        .Where(row => row.Id == eventId && row.ProcessedAt == null).ExecuteUpdateAsync(update =>
            update.SetProperty(row => row.Attempts, row => row.Attempts + 1)
                .SetProperty(row => row.NextAttemptAt, clock.GetUtcNow().AddMinutes(1))
                .SetProperty(row => row.LastError, INotificationEventSource.ProcessingFailureCode), cancellationToken);
}
