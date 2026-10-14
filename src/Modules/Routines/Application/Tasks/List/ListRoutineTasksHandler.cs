using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Tasks.List;

public sealed class ListRoutineTasksHandler(RoutinesDbContext context, ICareFacade care, RoutineTaskMapper mapper)
{
    public async Task<RoutineResult<PageDto<RoutineTaskDto>>> Handle(ListRoutineTasksQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return RoutineResult<PageDto<RoutineTaskDto>>.Failed(RoutineFailure.ResourceNotFound);
        var rows = context.RoutineTasks.AsNoTracking().Where(task => task.SeniorId == query.SeniorId && task.DeletedAt == null);
        if (query.Type is { } type) rows = rows.Where(task => task.Type == type);
        if (query.IsActive is { } active) rows = rows.Where(task => task.IsActive == active);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(task => task.Title.ToUpper().Contains(term) || task.Instructions.ToUpper().Contains(term));
        }
        var total = await rows.CountAsync(cancellationToken);
        var tasks = await rows.OrderBy(task => task.Title).ThenBy(task => task.Id)
            .Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToListAsync(cancellationToken);
        return RoutineResult<PageDto<RoutineTaskDto>>.Success(new(await mapper.MapAsync(tasks, cancellationToken), query.Page, query.Limit, total));
    }
}
