using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

public sealed class LocalMedicalFileStorage(IOptions<MedicalRecordOptions> options, IHostEnvironment environment) : IMedicalFileStorage
{
    private readonly string root = Path.GetFullPath(options.Value.StorageRoot, environment.ContentRootPath);

    public async Task<StoredMedicalFile> SaveAsync(MedicalUploadFile upload, CancellationToken cancellationToken)
    {
        var (name, mime) = MedicalUploadContent.Describe(upload, options.Value.MaxFileSizeBytes);
        var key = Guid.CreateVersion7().ToString("N");
        var temporary = FilePath(key + ".tmp");
        try
        {
            if (OperatingSystem.IsWindows()) Directory.CreateDirectory(root);
            else Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            await using var input = upload.OpenRead();
            long length;
            string checksum;
            var fileOptions = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 81_920,
                Options = FileOptions.Asynchronous
            };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var output = new FileStream(temporary, fileOptions))
            {
                (length, checksum) = await MedicalUploadContent.CopyAsync(input, output, mime, options.Value.MaxFileSizeBytes, cancellationToken);
                await output.FlushAsync(cancellationToken);
            }
            File.Move(temporary, FilePath(key));
            return new StoredMedicalFile(key, name, mime, length, checksum);
        }
        catch (Exception exception) when (exception is IOException and not BadHttpRequestException or UnauthorizedAccessException)
        {
            throw new MedicalStorageException(exception);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
        }
    }

    public Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            Stream stream = new FileStream(FilePath(objectKey), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 81_920, FileOptions.Asynchronous);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new MedicalStorageException(exception);
        }
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { File.Delete(FilePath(objectKey)); }
        catch (DirectoryNotFoundException) { }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { throw new MedicalStorageException(exception); }
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> FindOrphansAsync(DateTimeOffset olderThan, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root)) yield break;
        foreach (var path in Directory.EnumerateFiles(root))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Path.GetFileName(path);
            if (!IsKey(key) || File.GetLastWriteTimeUtc(path) >= olderThan.UtcDateTime) continue;
            yield return key;
            await Task.Yield();
        }
    }

    private string FilePath(string key) => IsKey(key) ? Path.Combine(root, key) : throw new InvalidOperationException("Invalid storage key.");
    private static bool IsKey(string key) => Guid.TryParseExact(key.EndsWith(".tmp", StringComparison.Ordinal) ? key[..^4] : key, "N", out _);
}
