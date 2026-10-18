using Microsoft.Extensions.Logging;

namespace Zpi.Modules.MedicalRecords.Application.Files;

public sealed class MedicalUploadService(IMedicalFileStorage storage, ILogger<MedicalUploadService> logger)
{
    public async Task<IReadOnlyList<StoredMedicalFile>> SaveAsync(IReadOnlyList<MedicalUploadFile> uploads, CancellationToken cancellationToken)
    {
        var saved = new List<StoredMedicalFile>();
        try
        {
            foreach (var upload in uploads) saved.Add(await storage.SaveAsync(upload, cancellationToken));
            return saved;
        }
        catch
        {
            foreach (var file in saved)
            {
                try { await storage.DeleteAsync(file.ObjectKey, CancellationToken.None); }
                catch (MedicalStorageException exception) { logger.LogError(exception, "Could not clean up an interrupted medical upload. Cleanup will retry."); }
            }
            throw;
        }
    }
}
