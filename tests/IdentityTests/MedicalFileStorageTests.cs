using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Infrastructure.Storage;

namespace Zpi.IdentityTests;

public sealed class MedicalFileStorageTests
{
    private static readonly byte[] Pdf = "%PDF-1.7"u8.ToArray();
    private static readonly string Key = S3MedicalFileStorage.KeyPrefix + Guid.CreateVersion7().ToString("N");

    [Fact]
    public void S3_client_accepts_inline_credentials_and_the_system_trust_store()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=unused",
            ["MedicalRecords:StorageProvider"] = "S3",
            ["MedicalRecords:S3:Endpoint"] = "https://storage.example.test",
            ["MedicalRecords:S3:Bucket"] = "records",
            ["MedicalRecords:S3:AccessKey"] = "coverage-test-access",
            ["MedicalRecords:S3:SecretKey"] = "coverage-test-secret",
            ["MedicalRecords:S3:CertificateAuthorityFile"] = ""
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new HostingEnvironment { ContentRootPath = Path.GetTempPath() });
        services.AddMedicalRecordsModule(configuration);
        using var provider = services.BuildServiceProvider();
        var config = Assert.IsType<AmazonS3Config>(provider.GetRequiredService<IAmazonS3>().Config);
        Assert.Equal("https://storage.example.test/", config.ServiceURL.TrimEnd('/') + "/");
        Assert.True(config.ForcePathStyle);
        Assert.Null(config.HttpClientFactory);
    }

    [Fact]
    public async Task Upload_deletes_object_when_storage_does_not_confirm_encryption()
    {
        using var client = new TestStorageClient();
        var storage = Storage(client);

        await Assert.ThrowsAsync<MedicalStorageException>(() => storage.SaveAsync(
            new("report.pdf", "application/pdf", Pdf.Length, () => new MemoryStream(Pdf)), CancellationToken.None));

        Assert.Equal(ServerSideEncryptionMethod.AES256, client.Uploaded!.ServerSideEncryptionMethod);
        Assert.Equal(("records", client.Uploaded.Key), client.Deleted);
    }

    [Theory]
    [InlineData(100, 101, true)]
    [InlineData(101, 100, true)]
    [InlineData(100, 101, false)]
    [InlineData(101, 100, false)]
    public async Task Upload_rejects_both_declared_and_actual_size_over_the_limit(int declared, int actual, bool s3)
    {
        using var client = new TestStorageClient();
        using var directory = new TemporaryStorageDirectory();
        var options = Options.Create(new MedicalRecordOptions { StorageRoot = directory.Path, MaxFileSizeBytes = 100 });
        IMedicalFileStorage storage = s3 ? Storage(client, 100) : new LocalMedicalFileStorage(options, new HostingEnvironment
        {
            ContentRootPath = Path.GetTempPath()
        });
        var contents = new byte[actual];
        Pdf.CopyTo(contents, 0);

        var exception = await Assert.ThrowsAsync<BadHttpRequestException>(() => storage.SaveAsync(
            new("report.pdf", "application/pdf", declared, () => new MemoryStream(contents)), CancellationToken.None));

        Assert.Equal(StatusCodes.Status413PayloadTooLarge, exception.StatusCode);
        Assert.Null(client.Uploaded);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory.Path));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("patient\nreport.pdf")]
    public async Task Upload_rejects_empty_or_control_character_file_names(string name)
    {
        using var client = new TestStorageClient();
        await Assert.ThrowsAsync<ValidationException>(() => Storage(client).SaveAsync(
            new(name, "application/pdf", Pdf.Length, () => new MemoryStream(Pdf)), CancellationToken.None));
        Assert.Null(client.Uploaded);
    }

    [Theory]
    [InlineData("application/pdf", "25504446")]
    [InlineData("image/png", "89504E470D0A1A")]
    [InlineData("image/jpeg", "FFD8")]
    [InlineData("image/jpeg", "00D8FF")]
    [InlineData("image/jpeg", "FF00FF")]
    [InlineData("image/jpeg", "FFD800")]
    [InlineData("text/plain", "255044462D")]
    public async Task Upload_rejects_incomplete_and_mismatched_file_signatures(string mime, string hex)
    {
        using var client = new TestStorageClient();
        var content = Convert.FromHexString(hex);
        await Assert.ThrowsAsync<ValidationException>(() => Storage(client).SaveAsync(
            new("report", mime, content.Length, () => new MemoryStream(content)), CancellationToken.None));
        Assert.Null(client.Uploaded);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(MedicalRecordOptions.MaximumFileSizeBytes + 1, true)]
    [InlineData(8, false)]
    [InlineData(16, true)]
    public async Task Download_rejects_invalid_object_metadata_or_truncated_content(long length, bool encrypted)
    {
        using var client = new TestStorageClient
        {
            Download = () => new GetObjectResponse
            {
                ContentLength = length,
                ServerSideEncryptionMethod = encrypted ? ServerSideEncryptionMethod.AES256 : ServerSideEncryptionMethod.None,
                ResponseStream = new MemoryStream(Pdf)
            }
        };

        var error = await Assert.ThrowsAsync<MedicalStorageException>(() => Storage(client).OpenReadAsync(Key, CancellationToken.None));
        Assert.IsType<IOException>(error.InnerException);
    }

    [Fact]
    public async Task Download_distinguishes_missing_object_timeout_and_request_cancellation()
    {
        using var client = new TestStorageClient();
        var storage = Storage(client);
        Assert.Null(await storage.OpenReadAsync(Key, CancellationToken.None));

        client.Download = () => throw new OperationCanceledException();
        var timeout = await Assert.ThrowsAsync<MedicalStorageException>(() => storage.OpenReadAsync(Key, CancellationToken.None));
        Assert.IsType<OperationCanceledException>(timeout.InnerException);
        await Assert.ThrowsAsync<OperationCanceledException>(() => storage.OpenReadAsync(Key, new CancellationToken(true)));
    }

    [Fact]
    public async Task Download_rejects_streams_that_exceed_the_size_limit_regardless_of_metadata()
    {
        using var client = new TestStorageClient
        {
            Download = () => new GetObjectResponse
            {
                ContentLength = MedicalRecordOptions.MaximumFileSizeBytes,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
                ResponseStream = new MemoryStream(new byte[MedicalRecordOptions.MaximumFileSizeBytes + 1])
            }
        };
        await Assert.ThrowsAsync<MedicalStorageException>(() => Storage(client).OpenReadAsync(Key, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Storage(client).OpenReadAsync("invalid", CancellationToken.None));
    }

    [Fact]
    public async Task Orphan_scan_paginates_and_ignores_recent_objects_and_unknown_keys()
    {
        using var client = new TestStorageClient();
        var cutoff = DateTimeOffset.UtcNow;
        var recent = S3MedicalFileStorage.KeyPrefix + Guid.CreateVersion7().ToString("N");
        var last = S3MedicalFileStorage.KeyPrefix + Guid.CreateVersion7().ToString("N");
        client.Pages.Enqueue(new ListObjectsV2Response
        {
            IsTruncated = true,
            NextContinuationToken = "next-page",
            S3Objects =
            [
                new() { Key = Key[3..], LastModified = cutoff.UtcDateTime.AddDays(-1) },
                new() { Key = recent[3..], LastModified = cutoff.UtcDateTime },
                new() { Key = "medical/unknown", LastModified = cutoff.UtcDateTime.AddDays(-1) }
            ]
        });
        client.Pages.Enqueue(new ListObjectsV2Response
        {
            S3Objects = [new() { Key = last[3..], LastModified = cutoff.UtcDateTime.AddDays(-1) }]
        });

        var keys = new List<string>();
        await foreach (var key in Storage(client).FindOrphansAsync(cutoff, CancellationToken.None)) keys.Add(key);

        Assert.Equal(new[] { Key, last }, keys);
        Assert.Equal(new[] { null, "next-page" }, client.ContinuationTokens);
    }

    [Fact]
    public async Task Storage_failures_during_cleanup_surface_to_the_caller_for_retry()
    {
        using var client = new TestStorageClient
        {
            CleanupError = new AmazonS3Exception("Unavailable") { StatusCode = HttpStatusCode.ServiceUnavailable }
        };
        var storage = Storage(client);
        var deletion = await Assert.ThrowsAsync<MedicalStorageException>(() => storage.DeleteAsync(Key, CancellationToken.None));
        Assert.Same(client.CleanupError, deletion.InnerException);
        await using var scan = storage.FindOrphansAsync(DateTimeOffset.UtcNow, CancellationToken.None).GetAsyncEnumerator();
        var listing = await Assert.ThrowsAsync<MedicalStorageException>(() => scan.MoveNextAsync().AsTask());
        Assert.Same(client.CleanupError, listing.InnerException);
    }

    [Fact]
    public async Task Upload_and_cleanup_use_s3_for_new_files_and_keep_legacy_local_files_accessible()
    {
        using var directory = new TemporaryStorageDirectory();
        using var client = new TestStorageClient { ConfirmEncryption = true };
        var options = Options.Create(new MedicalRecordOptions
        {
            StorageRoot = directory.Path,
            StorageProvider = MedicalStorageProvider.S3,
            S3 = new() { Bucket = "records" }
        });
        var local = new LocalMedicalFileStorage(options, new HostingEnvironment { ContentRootPath = Path.GetTempPath() });
        var remote = new S3MedicalFileStorage(new Lazy<IAmazonS3>(() => client), options);
        var combined = new MedicalFileStorage(local, remote, options);
        var upload = new MedicalUploadFile("report.pdf", "application/pdf", Pdf.Length, () => new MemoryStream(Pdf));
        var legacy = await local.SaveAsync(upload, CancellationToken.None);
        var stored = await combined.SaveAsync(upload, CancellationToken.None);
        Assert.True(S3MedicalFileStorage.IsKey(stored.ObjectKey));
        Assert.Equal(legacy.ChecksumSha256, stored.ChecksumSha256);
        Assert.Equal(legacy.SizeBytes, stored.SizeBytes);
        client.Download = () => new GetObjectResponse
        {
            ContentLength = Pdf.Length,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
            ResponseStream = new MemoryStream(Pdf)
        };
        await using var localFile = await combined.OpenReadAsync(legacy.ObjectKey, CancellationToken.None);
        await using var remoteFile = await combined.OpenReadAsync(stored.ObjectKey, CancellationToken.None);
        Assert.Equal(localFile!.Length, remoteFile!.Length);

        var cutoff = DateTimeOffset.UtcNow;
        File.SetLastWriteTimeUtc(Path.Combine(directory.Path, legacy.ObjectKey), cutoff.UtcDateTime.AddDays(-2));
        client.Pages.Enqueue(new ListObjectsV2Response());
        var keys = new List<string>();
        await foreach (var key in combined.FindOrphansAsync(cutoff, CancellationToken.None)) keys.Add(key);
        Assert.Equal(legacy.ObjectKey, Assert.Single(keys));
        await combined.DeleteAsync(stored.ObjectKey, CancellationToken.None);
        Assert.Equal(("records", stored.ObjectKey[3..]), client.Deleted);
    }

    [Fact]
    public async Task Local_storage_distinguishes_missing_files_from_an_invalid_storage_path()
    {
        using var directory = new TemporaryStorageDirectory();
        var key = Guid.CreateVersion7().ToString("N");
        var storagePath = Path.Combine(directory.Path, "storage");
        var storage = new LocalMedicalFileStorage(Options.Create(new MedicalRecordOptions { StorageRoot = storagePath }),
            new HostingEnvironment { ContentRootPath = Path.GetTempPath() });
        Assert.Null(await storage.OpenReadAsync(key, CancellationToken.None));
        await storage.DeleteAsync(key, CancellationToken.None);
        Directory.CreateDirectory(storagePath);
        Assert.Null(await storage.OpenReadAsync(key, CancellationToken.None));
        Directory.CreateDirectory(Path.Combine(storagePath, key));
        await Assert.ThrowsAsync<MedicalStorageException>(() => storage.OpenReadAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<MedicalStorageException>(() => storage.DeleteAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => storage.OpenReadAsync("../other-storage", CancellationToken.None));
    }

    private sealed class TemporaryStorageDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("zpi-medical-storage-").FullName;

        public void Dispose() => Directory.Delete(Path, true);
    }

    private static S3MedicalFileStorage Storage(TestStorageClient client, long limit = MedicalRecordOptions.MaximumFileSizeBytes) =>
        new(new Lazy<IAmazonS3>(() => client), Options.Create(new MedicalRecordOptions
        {
            MaxFileSizeBytes = limit,
            S3 = new() { Bucket = "records" }
        }));

    private sealed class TestStorageClient() : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
    {
        RegionEndpoint = Amazon.RegionEndpoint.EUCentral1
    })
    {
        public PutObjectRequest? Uploaded { get; private set; }
        public bool ConfirmEncryption { get; init; }
        public (string Bucket, string Key)? Deleted { get; private set; }
        public Func<GetObjectResponse> Download { get; set; } = () => throw new NoSuchKeyException("Missing");
        public AmazonS3Exception? CleanupError { get; init; }
        public Queue<ListObjectsV2Response> Pages { get; } = new();
        public List<string?> ContinuationTokens { get; } = [];

        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken)
        {
            Uploaded = request;
            return Task.FromResult(new PutObjectResponse
            {
                ServerSideEncryptionMethod = ConfirmEncryption ? ServerSideEncryptionMethod.AES256 : ServerSideEncryptionMethod.None
            });
        }

        public override Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, CancellationToken cancellationToken) =>
            Task.FromResult(Download());

        public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken)
        {
            if (CleanupError is not null) throw CleanupError;
            Deleted = (bucketName, key);
            return Task.FromResult(new DeleteObjectResponse());
        }

        public override Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request, CancellationToken cancellationToken)
        {
            if (CleanupError is not null) throw CleanupError;
            Assert.Equal("medical/", request.Prefix);
            ContinuationTokens.Add(request.ContinuationToken);
            return Task.FromResult(Pages.Dequeue());
        }
    }
}
