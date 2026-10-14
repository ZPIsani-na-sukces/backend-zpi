using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Application.Scheduling;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Tasks.Create;

public sealed class CreateRoutineTaskHandler(RoutinesDbContext context, ICareFacade care, RoutineTaskMapper mapper,
    OccurrenceGenerator generator, TimeProvider clock)
{
    public async Task<RoutineResult<RoutineTaskDto>> Handle(CreateRoutineTaskCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (command.Task.IsActive && await context.RoutineTasks.CountAsync(task => task.SeniorId == command.SeniorId &&
            task.IsActive && task.DeletedAt == null, cancellationToken) >= 50)
            return RoutineResult<RoutineTaskDto>.Failed(RoutineFailure.TaskLimitReached);
        var now = clock.GetUtcNow();
        var task = new RoutineTask { SeniorId = command.SeniorId, Title = "", Instructions = "",
            CreatedByAccountId = command.AccountId, UpdatedByAccountId = command.AccountId, CreatedAt = now, UpdatedAt = now };
        command.Task.ApplyTo(task);
        context.RoutineTasks.Add(task);
        var schedules = command.Task.Schedule.CreateRows(task.Id, task.ScheduleRevision);
        context.RoutineTaskSchedules.AddRange(schedules);
        await generator.GenerateAsync(task, schedules, now, now.AddDays(30), now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoutineResult<RoutineTaskDto>.Success(await mapper.MapAsync(task, cancellationToken));
    }
}
