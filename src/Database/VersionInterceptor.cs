using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Zpi.Database;

public sealed class VersionInterceptor : SaveChangesInterceptor
{
    private static void AdvanceVersions(DbContext? context)
    {
        if (context is null) return;
        context.ChangeTracker.DetectChanges();
        foreach (var entry in context.ChangeTracker.Entries())
        {
            var property = entry.Metadata.FindProperty("Version");
            if (property?.IsConcurrencyToken != true) continue;
            var version = entry.Property("Version");
            version.CurrentValue = entry.State switch
            {
                EntityState.Added => 1,
                EntityState.Modified => (int)version.OriginalValue! + 1,
                _ => version.CurrentValue
            };
        }
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AdvanceVersions(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        AdvanceVersions(eventData.Context);
        return ValueTask.FromResult(result);
    }
}
