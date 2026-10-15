using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Zpi.Domain;
using Zpi.Modules.Routines.Application.Reminders;

namespace Zpi.Modules.Routines.Infrastructure.Persistence;

internal sealed class AccountDataCleanup(RoutinesDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var recipient = JsonSerializer.Serialize(new { RecipientAccountId = accountId });
        await context.OutboxMessages.Where(row => row.Type == RoutineReminderEvent.Type && EF.Functions.JsonContains(row.Payload, recipient))
            .ExecuteDeleteAsync(cancellationToken);
        var tasks = context.RoutineTasks.Where(row => row.SeniorId == accountId).Select(row => row.Id);
        var occurrences = context.TaskOccurrences.Where(row => row.SeniorId == accountId || tasks.Contains(row.RoutineTaskId)).Select(row => row.Id);
        await context.TaskConfirmations.Where(row => occurrences.Contains(row.TaskOccurrenceId)).ExecuteDeleteAsync(cancellationToken);
        await context.TaskOccurrences.Where(row => row.SeniorId == accountId || tasks.Contains(row.RoutineTaskId)).ExecuteDeleteAsync(cancellationToken);
        await context.RoutineTaskSchedules.Where(row => tasks.Contains(row.RoutineTaskId)).ExecuteDeleteAsync(cancellationToken);
        await context.RoutineTasks.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
