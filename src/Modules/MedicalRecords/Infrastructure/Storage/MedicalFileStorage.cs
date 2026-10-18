using System.Runtime.CompilerServices;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

public sealed class MedicalFileStorage(LocalMedicalFileStorage local, S3MedicalFileStorage s3,
    IOptions<MedicalRecordOptions> options) : IMedicalFileStorage
{
    public Task<StoredMedicalFile> SaveAsync(MedicalUploadFile upload, CancellationToken cancellationToken) =>
        Current.SaveAsync(upload, cancellationToken);

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken) => ForKey(key).OpenReadAsync(key, cancellationToken);
    public Task DeleteAsync(string key, CancellationToken cancellationToken) => ForKey(key).DeleteAsync(key, cancellationToken);

    public async IAsyncEnumerable<string> FindOrphansAsync(DateTimeOffset olderThan, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var key in local.FindOrphansAsync(olderThan, cancellationToken)) yield return key;
        if (options.Value.StorageProvider == MedicalStorageProvider.S3)
            await foreach (var key in s3.FindOrphansAsync(olderThan, cancellationToken)) yield return key;
    }

    private IMedicalFileStorage Current => options.Value.StorageProvider == MedicalStorageProvider.S3 ? s3 : local;
    private IMedicalFileStorage ForKey(string key) => key.StartsWith(S3MedicalFileStorage.StoragePrefix, StringComparison.Ordinal) ? s3 : local;
}
