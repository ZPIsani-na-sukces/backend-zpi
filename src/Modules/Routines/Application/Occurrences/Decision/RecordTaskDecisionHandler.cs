using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Routines.Domain;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Occurrences.Decision;

public sealed class RecordTaskDecisionHandler(RoutinesDbContext context, ICareFacade care, TaskOccurrenceMapper mapper, TimeProvider clock)
{
    public async Task<RoutineResult<TaskOccurrenceDto>> Handle(RecordTaskDecisionCommand command, CancellationToken cancellationToken)
    {
        if (command.AccountId != command.SeniorId || !await care.CanAccessSeniorAsync(command.AccountId, command.SeniorId, cancellationToken))
            return RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var occurrence = await context.TaskOccurrences.SingleOrDefaultAsync(row => row.Id == command.OccurrenceId &&
            row.SeniorId == command.SeniorId, cancellationToken);
        if (occurrence is null) return RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.ResourceNotFound);
        var previous = await context.TaskConfirmations.AsNoTracking().SingleOrDefaultAsync(row => row.TaskOccurrenceId == occurrence.Id, cancellationToken);
        if (previous is not null)
            return previous.Decision == command.Request.Decision
                ? RoutineResult<TaskOccurrenceDto>.Success(await mapper.MapAsync(occurrence, cancellationToken))
                : RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.DecisionAlreadyRecorded);
        if (occurrence.Status == OccurrenceStatus.Cancelled)
            return RoutineResult<TaskOccurrenceDto>.Failed(RoutineFailure.OccurrenceCancelled);
        var now = clock.GetUtcNow();
        occurrence.Status = command.Request.Decision == TaskDecision.Confirmed ? OccurrenceStatus.Confirmed : OccurrenceStatus.Skipped;
        occurrence.UpdatedAt = now;
        context.TaskConfirmations.Add(new TaskConfirmation { TaskOccurrenceId = occurrence.Id, Decision = command.Request.Decision,
            PerformedAt = DateTimeOffset.FromUnixTimeMilliseconds(command.Request.PerformedAt.ToUnixTimeMilliseconds()),
            ReceivedAt = now, RecordedByAccountId = command.AccountId });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RoutineResult<TaskOccurrenceDto>.Success(await mapper.MapAsync(occurrence, cancellationToken));
    }
}
