using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Tasks.Get;

public sealed class GetRoutineTaskHandler(RoutinesDbContext context, ICareFacade care, RoutineTaskMapper mapper)
{
    public async Task<RoutineResult<RoutineTaskDto>> Handle(GetRoutineTaskQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.ResourceNotFound);
        var task = await context.RoutineTasks.AsNoTracking().SingleOrDefaultAsync(row => row.Id == query.TaskId &&
            row.SeniorId == query.SeniorId && row.DeletedAt == null, cancellationToken);
        return task is null ? RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.ResourceNotFound)
            : RoutineResult<RoutineTaskDto>.Success(await mapper.MapAsync(task, cancellationToken));
    }
}
