using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Tasks;

public sealed class RoutineTaskMapper(RoutinesDbContext context, IIdentityFacade identity)
{
    public async Task<IReadOnlyList<RoutineTaskDto>> MapAsync(IReadOnlyList<RoutineTask> tasks, CancellationToken cancellationToken)
    {
        if (tasks.Count == 0) return [];
        var ids = tasks.Select(task => task.Id).ToArray();
        var revisions = tasks.Select(task => task.ScheduleRevision).Distinct().ToArray();
        var schedules = await context.RoutineTaskSchedules.AsNoTracking()
            .Where(row => ids.Contains(row.RoutineTaskId) && revisions.Contains(row.Revision))
            .ToListAsync(cancellationToken);
        var authors = await identity.GetAccountSummariesAsync(tasks.Select(task => task.CreatedByAccountId).Distinct().ToArray(), cancellationToken);
        return tasks.Select(task =>
        {
            var rows = schedules.Where(row => row.RoutineTaskId == task.Id && row.Revision == task.ScheduleRevision).OrderBy(row => row.TimeOfDay).ToArray();
            var schedule = new RoutineScheduleInputDto(rows[0].TimeZone, rows[0].DaysOfWeek, rows.Select(row => row.TimeOfDay).ToArray());
            authors.TryGetValue(task.CreatedByAccountId, out var author);
            return new RoutineTaskDto(task.Id, task.SeniorId, task.Type.ToCamelCase(), task.Title, task.Instructions,
                task.StartsOn, task.EndsOn, task.IsActive, schedule, task.ScheduleRevision, [15, 0], author,
                task.CreatedAt, task.UpdatedAt, task.Version);
        }).ToArray();
    }

    public async Task<RoutineTaskDto> MapAsync(RoutineTask task, CancellationToken cancellationToken) =>
        (await MapAsync([task], cancellationToken))[0];
}
