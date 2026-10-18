using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Infrastructure.Storage;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.IdentityTests;

public sealed partial class MedicalRecordTests
{
    [S3PostgresFact]
    public async Task S3_upload_is_encrypted_private_and_download_supports_range_and_cleanup()
    {
        await using var factory = S3Factory();
        using var health = factory.CreateClient();
        await Status(await health.GetAsync("/health/ready"), 200);
        using var senior = await Actor(factory, AccountRole.Senior);
        using var caretaker = await Actor(factory, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var record = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "private.pdf"));
        var stored = Assert.Single(await StoredFiles(factory, record.RecordId));
        Assert.True(S3MedicalFileStorage.IsKey(stored.ObjectKey));
        var client = factory.Services.GetRequiredService<IAmazonS3>();
        var config = S3Configuration();
        var bucket = config["MedicalRecords:S3:Bucket"]!;
        var metadata = await client.GetObjectMetadataAsync(bucket, stored.ObjectKey[3..]);
        Assert.Equal(ServerSideEncryptionMethod.AES256, metadata.ServerSideEncryptionMethod);
        using var request = new HttpRequestMessage(HttpMethod.Get, ContentPath(senior.Id, record.RecordId, stored.Id));
        request.Headers.Range = new RangeHeaderValue(0, 4);
        using var partial = await senior.Client.SendAsync(request);
        await Status(partial, 206);
        Assert.Equal("%PDF-", await partial.Content.ReadAsStringAsync());
        Assert.True(partial.Headers.CacheControl!.NoStore);
        using var anonymous = AnonymousStorageClient(config);
        using var forbidden = await anonymous.GetAsync(config["MedicalRecords:S3:Endpoint"] + "/" + bucket + "/" + stored.ObjectKey[3..]);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        await Status(await Change(caretaker, HttpMethod.Delete, PathFor(senior.Id, record.RecordId), 1), 204);
        await Cleanup(factory);
        var missing = await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => client.GetObjectAsync(bucket, stored.ObjectKey[3..]));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IMedicalFileStorage>().OpenReadAsync(stored.ObjectKey, CancellationToken.None));
    }

    [S3PostgresFact]
    public async Task S3_policy_denies_unencrypted_upload_other_buckets_and_admin_operations()
    {
        await using var factory = S3Factory();
        var client = factory.Services.GetRequiredService<IAmazonS3>();
        var bucket = S3Configuration()["MedicalRecords:S3:Bucket"]!;
        var exception = await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = "medical/" + Guid.CreateVersion7().ToString("N"),
            ContentBody = "must not be stored without encryption"
        }));
        Assert.Equal(HttpStatusCode.Forbidden, exception.StatusCode);
        var foreign = await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => client.ListObjectsV2Async(new ListObjectsV2Request
        { BucketName = "foreign-" + Guid.CreateVersion7().ToString("N"), MaxKeys = 1 }));
        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        var admin = await Assert.ThrowsAnyAsync<AmazonS3Exception>(() => client.PutBucketPolicyAsync(new PutBucketPolicyRequest
        { BucketName = bucket, Policy = "{\"Version\":\"2012-10-17\",\"Statement\":[]}" }));
        Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
    }

    [S3PostgresFact]
    public async Task S3_switch_preserves_local_documents_and_detects_changed_object_content()
    {
        await using var local = Factory();
        using var senior = await Actor(local, AccountRole.Senior);
        using var caretaker = await Actor(local, AccountRole.Caretaker);
        await Link(senior, caretaker);
        var legacy = await Create(caretaker, senior.Id, (Pdf, "application/pdf", "legacy.pdf"));
        await using var s3 = S3Factory();
        using var api = s3.CreateClient();
        api.DefaultRequestHeaders.Authorization = senior.Client.DefaultRequestHeaders.Authorization;
        using var download = await api.GetAsync(ContentPath(senior.Id, legacy.RecordId, legacy.Files[0].FileId));
        await Status(download, 200);
        Assert.Equal(Pdf, await download.Content.ReadAsByteArrayAsync());
        using var writer = s3.CreateClient();
        writer.DefaultRequestHeaders.Authorization = caretaker.Client.DefaultRequestHeaders.Authorization;
        var record = await Create(new(caretaker.Id, caretaker.Phone, writer), senior.Id, (Pdf, "application/pdf", "new.pdf"));
        var stored = Assert.Single(await StoredFiles(s3, record.RecordId));
        var client = s3.Services.GetRequiredService<IAmazonS3>();
        try
        {
            await client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = S3Configuration()["MedicalRecords:S3:Bucket"], Key = stored.ObjectKey[3..],
                ContentBody = "%PDF-1.4 changed content", ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            });
            using var broken = await api.GetAsync(ContentPath(senior.Id, record.RecordId, stored.Id));
            await Status(broken, 503);
            Assert.DoesNotContain("checksum", await broken.Content.ReadAsStringAsync());
        }
        finally { await client.DeleteObjectAsync(S3Configuration()["MedicalRecords:S3:Bucket"], stored.ObjectKey[3..]); }
    }

    [S3PostgresFact]
    public async Task S3_rejects_untrusted_certificates_without_writing_medical_metadata()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Unrelated storage CA", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = System.IO.Path.Combine(fixture.MedicalStorageRoot, Guid.CreateVersion7().ToString("N") + ".pem");
        Directory.CreateDirectory(fixture.MedicalStorageRoot);
        await File.WriteAllTextAsync(path, certificate.ExportCertificatePem());
        try
        {
            await using var factory = S3Factory(path);
            using var health = factory.CreateClient();
            await Status(await health.GetAsync("/health/ready"), 503);
            using var senior = await Actor(factory, AccountRole.Senior);
            using var caretaker = await Actor(factory, AccountRole.Caretaker);
            await Link(senior, caretaker);
            using var form = Form(Metadata(), false, (Pdf, "application/pdf", "private.pdf"));
            using var response = await caretaker.Client.PostAsync(Base(senior.Id), form);
            await Status(response, 503);
            await using var scope = factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<MedicalRecordsDbContext>().MedicalRecords.AnyAsync(row => row.SeniorId == senior.Id));
        }
        finally { File.Delete(path); }
    }

    private WebApplicationFactory<Program> S3Factory(string? caFile = null) => Factory().WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = S3Configuration();
            if (caFile is not null) settings["MedicalRecords:S3:CertificateAuthorityFile"] = caFile;
            configuration.AddInMemoryCollection(settings);
        }));

    private static Dictionary<string, string?> S3Configuration()
    {
        var settings = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Environment.GetEnvironmentVariable("ZPI_TEST_S3_CONFIG")!))!;
        settings["MedicalRecords:StorageProvider"] = "S3";
        return settings;
    }

    private static HttpClient AnonymousStorageClient(Dictionary<string, string?> config)
    {
        var chain = new X509ChainPolicy { TrustMode = X509ChainTrustMode.CustomRootTrust, RevocationMode = X509RevocationMode.NoCheck };
        chain.CustomTrustStore.Add(X509Certificate2.CreateFromPem(File.ReadAllText(config["MedicalRecords:S3:CertificateAuthorityFile"]!)));
        return new HttpClient(new SocketsHttpHandler { SslOptions = new() { CertificateChainPolicy = chain } });
    }
}

public sealed class S3PostgresFactAttribute : FactAttribute
{
    public S3PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_S3_CONFIG")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_DATABASE")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ZPI_TEST_REDIS")))
            Skip = "Requires isolated PostgreSQL, Redis and S3 configuration.";
    }
}
