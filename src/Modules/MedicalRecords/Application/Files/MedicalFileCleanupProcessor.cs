using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Files;

public sealed class MedicalFileCleanupProcessor(MedicalRecordsDbContext context, IMedicalFileStorage storage,
    TimeProvider clock, ILogger<MedicalFileCleanupProcessor> logger)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var files = await context.MedicalFiles
            .Where(row => row.DeletedAt != null && row.PurgedAt == null)
            .OrderBy(row => row.DeletedAt)
            .ThenBy(row => row.Id)
            .Take(100)
            .ToListAsync(cancellationToken);
        foreach (var file in files)
        {
            try
            {
                await storage.DeleteAsync(file.ObjectKey, cancellationToken);
                file.PurgedAt = clock.GetUtcNow();
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (MedicalStorageException exception) { logger.LogError(exception, "Medical file cleanup failed for {FileId}. The next run will retry.", file.Id); }
        }
        var candidates = new List<string>(100);
        var removed = 0;
        await foreach (var key in storage.FindOrphansAsync(clock.GetUtcNow().AddDays(-1), cancellationToken))
        {
            candidates.Add(key);
            if (candidates.Count < 100) continue;
            removed += await PurgeOrphansAsync(candidates, cancellationToken);
            candidates.Clear();
            if (removed >= 100) return;
        }
        if (candidates.Count > 0) await PurgeOrphansAsync(candidates, cancellationToken);
    }

    private async Task<int> PurgeOrphansAsync(IReadOnlyList<string> candidates, CancellationToken cancellationToken)
    {
        var referenced = (await context.MedicalFiles
            .Where(row => candidates.Contains(row.ObjectKey))
            .Select(row => row.ObjectKey)
            .ToListAsync(cancellationToken)).ToHashSet();
        var removed = 0;
        foreach (var key in candidates.Where(key => !referenced.Contains(key)))
        {
            await storage.DeleteAsync(key, cancellationToken);
            removed++;
        }
        return removed;
    }
}
