using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Routines.Infrastructure.Persistence;

namespace Zpi.Modules.Routines.Application.Scheduling;

public sealed class OccurrenceProcessor(RoutinesDbContext context, OccurrenceGenerator generator,
    IIdentityFacade identity, TimeProvider clock)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var seniorIds = await context.RoutineTasks.AsNoTracking()
            .Where(task => task.IsActive && task.DeletedAt == null)
            .Select(task => task.SeniorId).Distinct().ToArrayAsync(cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync(seniorIds, cancellationToken);
        foreach (var seniorId in seniorIds.Where(accounts.ContainsKey))
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var now = clock.GetUtcNow();
            var tasks = await context.RoutineTasks.AsNoTracking()
                .Where(task => task.SeniorId == seniorId && task.IsActive && task.DeletedAt == null)
                .ToListAsync(cancellationToken);
            var ids = tasks.Select(task => task.Id).ToArray();
            var revisions = tasks.Select(task => task.ScheduleRevision).Distinct().ToArray();
            var schedules = await context.RoutineTaskSchedules.AsNoTracking()
                .Where(row => ids.Contains(row.RoutineTaskId) && revisions.Contains(row.Revision))
                .ToListAsync(cancellationToken);
            await generator.GenerateBatchAsync(tasks, schedules, now.AddDays(-30), now.AddDays(30), now, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
