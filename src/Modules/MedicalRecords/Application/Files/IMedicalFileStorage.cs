namespace Zpi.Modules.MedicalRecords.Application.Files;

public interface IMedicalFileStorage
{
    Task<StoredMedicalFile> SaveAsync(MedicalUploadFile upload, CancellationToken cancellationToken);
    Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken);
    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
    IAsyncEnumerable<string> FindOrphansAsync(DateTimeOffset olderThan, CancellationToken cancellationToken);
}

public sealed record MedicalUploadFile(string Name, string MimeType, long Length, Func<Stream> OpenRead);
public sealed record StoredMedicalFile(string ObjectKey, string OriginalName, string MimeType, long SizeBytes, string ChecksumSha256);
public sealed class MedicalStorageException(Exception inner) : Exception("Medical file storage is unavailable.", inner);
