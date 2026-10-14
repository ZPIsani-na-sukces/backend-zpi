using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Occurrences;

public sealed class TaskOccurrenceMapper(RoutinesDbContext context, TimeProvider clock)
{
    public async Task<IReadOnlyList<TaskOccurrenceDto>> MapAsync(IReadOnlyList<TaskOccurrence> occurrences, CancellationToken cancellationToken)
    {
        if (occurrences.Count == 0) return [];
        var ids = occurrences.Select(row => row.Id).ToArray();
        var decisions = await context.TaskConfirmations.AsNoTracking()
            .Where(row => ids.Contains(row.TaskOccurrenceId)).ToDictionaryAsync(row => row.TaskOccurrenceId, cancellationToken);
        var now = clock.GetUtcNow();
        return occurrences.Select(row =>
        {
            var status = row.Status == OccurrenceStatus.Pending && row.ScheduledAt <= now.AddHours(-24)
                ? OccurrenceStatus.Missed : row.Status;
            var decision = decisions.TryGetValue(row.Id, out var confirmation)
                ? new TaskDecisionDto(confirmation.Decision.ToCamelCase(), confirmation.PerformedAt, confirmation.ReceivedAt, confirmation.RecordedByAccountId) : null;
            return new TaskOccurrenceDto(row.Id, row.SeniorId, row.RoutineTaskId, row.ScheduleRevision,
                row.Type.ToCamelCase(), row.Title, row.Instructions, row.ScheduledAt, row.TimeZone, status.ToCamelCase(),
                [row.ScheduledAt.AddMinutes(-15), row.ScheduledAt], decision, row.UpdatedAt);
        }).ToArray();
    }

    public async Task<TaskOccurrenceDto> MapAsync(TaskOccurrence occurrence, CancellationToken cancellationToken) =>
        (await MapAsync([occurrence], cancellationToken))[0];
}
