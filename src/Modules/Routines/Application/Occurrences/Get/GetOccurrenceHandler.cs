using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Occurrences.Get;

public sealed class GetOccurrenceHandler(RoutinesDbContext context, ICareFacade care, TaskOccurrenceMapper mapper)
{
    public async Task<RoutineResult<TaskOccurrenceDto>> Handle(GetOccurrenceQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.ResourceNotFound);
        var occurrence = await context.TaskOccurrences.AsNoTracking().SingleOrDefaultAsync(row => row.Id == query.OccurrenceId &&
            row.SeniorId == query.SeniorId, cancellationToken);
        return occurrence is null ? RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.ResourceNotFound)
            : RoutineResult<TaskOccurrenceDto>.Success(await mapper.MapAsync(occurrence, cancellationToken));
    }
}
