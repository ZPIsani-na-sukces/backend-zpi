using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Occurrences.List;

public sealed class ListOccurrencesHandler(RoutinesDbContext context, ICareFacade care, TaskOccurrenceMapper mapper, TimeProvider clock)
{
    public async Task<RoutineResult<PageDto<TaskOccurrenceDto>>> Handle(ListOccurrencesQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return RoutineResult<PageDto<TaskOccurrenceDto>>.Failed(RoutineFailure.ResourceNotFound);
        var from = query.From.ToUniversalTime();
        var to = query.To.ToUniversalTime();
        var rows = context.TaskOccurrences.AsNoTracking().Where(row => row.SeniorId == query.SeniorId &&
            row.ScheduledAt >= from && row.ScheduledAt < to);
        if (query.Type is { } type) rows = rows.Where(row => row.Type == type);
        if (query.TaskId is { } taskId) rows = rows.Where(row => row.RoutineTaskId == taskId);
        var cutoff = clock.GetUtcNow().AddHours(-24);
        if (query.Status == OccurrenceStatus.Pending)
            rows = rows.Where(row => row.Status == OccurrenceStatus.Pending && row.ScheduledAt > cutoff);
        else if (query.Status == OccurrenceStatus.Missed)
            rows = rows.Where(row => row.Status == OccurrenceStatus.Missed || row.Status == OccurrenceStatus.Pending && row.ScheduledAt <= cutoff);
        else if (query.Status is { } status) rows = rows.Where(row => row.Status == status);
        var total = await rows.CountAsync(cancellationToken);
        var occurrences = await rows.OrderBy(row => row.ScheduledAt).ThenBy(row => row.Id)
            .Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToListAsync(cancellationToken);
        return RoutineResult<PageDto<TaskOccurrenceDto>>.Success(new(await mapper.MapAsync(occurrences, cancellationToken), query.Page, query.Limit, total));
    }
}
