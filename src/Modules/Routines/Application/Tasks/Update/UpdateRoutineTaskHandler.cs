using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Tasks.Update;

public sealed class UpdateRoutineTaskHandler(RoutinesDbContext context, ICareFacade care, RoutineTaskMapper mapper,
    OccurrenceGenerator generator, TimeProvider clock)
{
    public async Task<RoutineResult<RoutineTaskDto>> Handle(UpdateRoutineTaskCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var task = await context.RoutineTasks.SingleOrDefaultAsync(row => row.Id == command.TaskId && row.SeniorId == command.SeniorId &&
            row.DeletedAt == null, cancellationToken);
        if (task is null) return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.ResourceNotFound);
        if (task.Version != command.ExpectedVersion) return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.VersionMismatch);
        if (!task.IsActive && command.Task.IsActive && await context.RoutineTasks.CountAsync(row => row.SeniorId == command.SeniorId &&
            row.IsActive && row.DeletedAt == null, cancellationToken) >= 50)
            return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.TaskLimitReached);
        var schedules = await context.RoutineTaskSchedules.Where(row => row.RoutineTaskId == task.Id &&
            row.Revision == task.ScheduleRevision).ToListAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var changed = command.Task.ChangesDefinition(task, schedules);
        command.Task.ApplyTo(task);
        task.UpdatedAt = now;
        task.UpdatedByAccountId = command.AccountId;
        context.Entry(task).Property(row => row.UpdatedAt).IsModified = true;
        if (changed)
        {
            task.ScheduleRevision++;
            await generator.CancelFutureAsync(task.Id, now, cancellationToken);
            var replacement = command.Task.Schedule.CreateRows(task.Id, task.ScheduleRevision);
            context.RoutineTaskSchedules.AddRange(replacement);
            await generator.GenerateAsync(task, replacement, now, now.AddDays(30), now, cancellationToken);
        }
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoutineResult<RoutineTaskDto>.Success(await mapper.MapAsync(task, cancellationToken));
    }
}
