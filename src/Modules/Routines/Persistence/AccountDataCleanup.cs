using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.Routines.Persistence;

internal sealed class AccountDataCleanup(RoutinesDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var tasks = context.RoutineTasks.Where(row => row.SeniorId == accountId).Select(row => row.Id);
        var occurrences = context.TaskOccurrences.Where(row => row.SeniorId == accountId || tasks.Contains(row.RoutineTaskId)).Select(row => row.Id);
        await context.TaskConfirmations.Where(row => occurrences.Contains(row.TaskOccurrenceId)).ExecuteDeleteAsync(cancellationToken);
        await context.TaskOccurrences.Where(row => row.SeniorId == accountId || tasks.Contains(row.RoutineTaskId)).ExecuteDeleteAsync(cancellationToken);
        await context.RoutineTaskSchedules.Where(row => tasks.Contains(row.RoutineTaskId)).ExecuteDeleteAsync(cancellationToken);
        await context.RoutineTasks.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
