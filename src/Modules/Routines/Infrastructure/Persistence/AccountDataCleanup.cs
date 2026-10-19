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
        await context.RoutineTasks.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
