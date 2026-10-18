using System.Runtime.CompilerServices;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

public sealed class S3MedicalFileStorage(Lazy<IAmazonS3> client, IOptions<MedicalRecordOptions> options) : IMedicalFileStorage
{
    public const string StoragePrefix = "s3/";
    public const string KeyPrefix = StoragePrefix + "medical/";
    private const string ObjectPrefix = "medical/";

    public async Task<StoredMedicalFile> SaveAsync(MedicalUploadFile upload, CancellationToken cancellationToken)
    {
        var (name, mime) = MedicalUploadContent.Describe(upload, options.Value.MaxFileSizeBytes);
        try
        {
            await using var input = upload.OpenRead();
            await using var buffer = new MemoryStream((int)upload.Length);
            var (length, checksum) = await MedicalUploadContent.CopyAsync(input, buffer, mime, options.Value.MaxFileSizeBytes, cancellationToken);
            buffer.Position = 0;
            var key = KeyPrefix + Guid.CreateVersion7().ToString("N");
            var response = await client.Value.PutObjectAsync(new PutObjectRequest
            {
                BucketName = options.Value.S3.Bucket,
                Key = ObjectKey(key),
                InputStream = buffer,
                AutoCloseStream = false,
                ContentType = mime,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            }, cancellationToken);
            if (response.ServerSideEncryptionMethod != ServerSideEncryptionMethod.AES256)
            {
                await client.Value.DeleteObjectAsync(options.Value.S3.Bucket, ObjectKey(key), cancellationToken);
                throw new IOException("Storage did not confirm server-side encryption.");
            }
            return new StoredMedicalFile(key, name, mime, length, checksum);
        }
        catch (Exception exception) when (IsStorageError(exception, cancellationToken)) { throw new MedicalStorageException(exception); }
    }

    public async Task<Stream?> OpenReadAsync(string objectKey, CancellationToken cancellationToken)
    {
        MemoryStream? buffer = null;
        try
        {
            using var response = await client.Value.GetObjectAsync(options.Value.S3.Bucket, ObjectKey(objectKey), cancellationToken);
            if (response.ContentLength <= 0 || response.ContentLength > MedicalRecordOptions.MaximumFileSizeBytes ||
                response.ServerSideEncryptionMethod != ServerSideEncryptionMethod.AES256)
                throw new IOException("Invalid stored medical object.");
            buffer = new MemoryStream((int)response.ContentLength);
            var chunk = new byte[81_920];
            int count;
            while ((count = await response.ResponseStream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + count > MedicalRecordOptions.MaximumFileSizeBytes) throw new IOException("Stored object exceeds the file limit.");
                await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
            }
            if (buffer.Length != response.ContentLength) throw new IOException("Incomplete stored medical object.");
            buffer.Position = 0;
            return buffer;
        }
        catch (NoSuchKeyException) { return null; }
        catch (Exception exception)
        {
            if (buffer is not null) await buffer.DisposeAsync();
            if (IsStorageError(exception, cancellationToken)) throw new MedicalStorageException(exception);
            throw;
        }
    }

    public async Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        try { await client.Value.DeleteObjectAsync(options.Value.S3.Bucket, ObjectKey(objectKey), cancellationToken); }
        catch (Exception exception) when (IsStorageError(exception, cancellationToken)) { throw new MedicalStorageException(exception); }
    }

    public async IAsyncEnumerable<string> FindOrphansAsync(DateTimeOffset olderThan, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? continuation = null;
        do
        {
            ListObjectsV2Response response;
            try
            {
                response = await client.Value.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = options.Value.S3.Bucket,
                    Prefix = ObjectPrefix,
                    MaxKeys = 1000,
                    ContinuationToken = continuation
                }, cancellationToken);
            }
            catch (Exception exception) when (IsStorageError(exception, cancellationToken)) { throw new MedicalStorageException(exception); }
            foreach (var item in response.S3Objects ?? [])
                if (item.LastModified < olderThan.UtcDateTime && IsKey(StoragePrefix + item.Key)) yield return StoragePrefix + item.Key;
            continuation = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuation is not null);
    }

    public static bool IsKey(string key) => key.StartsWith(KeyPrefix, StringComparison.Ordinal) && Guid.TryParseExact(key[KeyPrefix.Length..], "N", out _);
    private static string ObjectKey(string key) => IsKey(key) ? key[StoragePrefix.Length..] : throw new InvalidOperationException("Invalid S3 storage key.");
    private static bool IsStorageError(Exception exception, CancellationToken cancellationToken) =>
        exception is AmazonS3Exception or HttpRequestException or IOException ||
        exception is OperationCanceledException && !cancellationToken.IsCancellationRequested;
}
