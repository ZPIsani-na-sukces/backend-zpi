using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Scheduling;

public sealed class OccurrenceGenerator(RoutinesDbContext context)
{
    public Task GenerateAsync(RoutineTask task, IReadOnlyList<RoutineTaskSchedule> schedules,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken cancellationToken) =>
        GenerateBatchAsync([task], schedules, from, to, now, cancellationToken);

    public async Task GenerateBatchAsync(IReadOnlyList<RoutineTask> tasks, IReadOnlyList<RoutineTaskSchedule> schedules,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0) return;
        var ids = tasks.Select(task => task.Id).ToArray();
        var existing = await context.TaskOccurrences.AsNoTracking()
            .Where(row => ids.Contains(row.RoutineTaskId) && row.ScheduledAt >= from && row.ScheduledAt < to)
            .Select(row => new { row.RoutineTaskId, row.ScheduleRevision, row.ScheduledAt, row.Status })
            .ToListAsync(cancellationToken);
        var occurrencesByTask = existing.ToLookup(row => row.RoutineTaskId);
        var schedulesByTask = schedules.ToLookup(row => row.RoutineTaskId);
        foreach (var task in tasks.Where(task => task.IsActive && task.DeletedAt == null))
        {
            var known = occurrencesByTask[task.Id].Where(row => row.ScheduleRevision == task.ScheduleRevision ||
                row.Status == OccurrenceStatus.Confirmed || row.Status == OccurrenceStatus.Skipped)
                .Select(row => row.ScheduledAt).ToHashSet();
            var current = schedulesByTask[task.Id].Where(row => row.Revision == task.ScheduleRevision).OrderBy(row => row.TimeOfDay).ToArray();
            Generate(task, current, known, from < task.CreatedAt ? task.CreatedAt : from, to, now);
        }
    }

    private void Generate(RoutineTask task, IReadOnlyList<RoutineTaskSchedule> schedules, HashSet<DateTimeOffset> known,
        DateTimeOffset from, DateTimeOffset to, DateTimeOffset now)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(schedules[0].TimeZone);
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(from, zone).DateTime);
        var last = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(to, zone).DateTime);
        if (first < task.StartsOn) first = task.StartsOn;
        if (task.EndsOn is { } end && last > end) last = end;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            var weekday = (int)date.DayOfWeek;
            if (weekday == 0) weekday = 7;
            foreach (var schedule in schedules)
            {
                if (!schedule.DaysOfWeek.Contains(weekday)) continue;
                var at = LocalScheduleTime.Resolve(date, schedule.TimeOfDay, zone);
                if (at < from || at >= to || !known.Add(at)) continue;
                context.TaskOccurrences.Add(new TaskOccurrence { SeniorId = task.SeniorId, RoutineTaskId = task.Id,
                    ScheduleRevision = task.ScheduleRevision, LocalDate = date, LocalTime = schedule.TimeOfDay,
                    ScheduledAt = at, TimeZone = schedule.TimeZone, Type = task.Type, Title = task.Title,
                    Instructions = task.Instructions, Status = OccurrenceStatus.Pending, UpdatedAt = now });
            }
        }
    }

    public async Task CancelFutureAsync(Guid taskId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var future = await context.TaskOccurrences.Where(row => row.RoutineTaskId == taskId && row.ScheduledAt >= now &&
            (row.Status == OccurrenceStatus.Pending || row.Status == OccurrenceStatus.Missed)).ToListAsync(cancellationToken);
        foreach (var occurrence in future)
        {
            occurrence.Status = OccurrenceStatus.Cancelled;
            occurrence.UpdatedAt = now;
        }
    }
}
