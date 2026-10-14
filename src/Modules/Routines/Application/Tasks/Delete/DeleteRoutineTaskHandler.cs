using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Tasks.Delete;

public sealed class DeleteRoutineTaskHandler(RoutinesDbContext context, ICareFacade care, OccurrenceGenerator generator, TimeProvider clock)
{
    public async Task<RoutineResult<bool>> Handle(DeleteRoutineTaskCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return RoutineResult<bool>.Failed(RoutineFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var task = await context.RoutineTasks.SingleOrDefaultAsync(row => row.Id == command.TaskId && row.SeniorId == command.SeniorId &&
            row.DeletedAt == null, cancellationToken);
        if (task is null) return RoutineResult<bool>.Failed(RoutineFailure.ResourceNotFound);
        if (task.Version != command.ExpectedVersion) return RoutineResult<bool>.Failed(RoutineFailure.VersionMismatch);
        var now = clock.GetUtcNow();
        task.IsActive = false;
        task.DeletedAt = now;
        task.DeletedByAccountId = command.AccountId;
        task.UpdatedAt = now;
        task.UpdatedByAccountId = command.AccountId;
        await generator.CancelFutureAsync(task.Id, now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoutineResult<bool>.Success(true);
    }
}
