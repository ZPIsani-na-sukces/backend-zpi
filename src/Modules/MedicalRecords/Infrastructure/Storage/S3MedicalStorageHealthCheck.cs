using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Zpi.Modules.MedicalRecords.Application;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

internal sealed class S3MedicalStorageHealthCheck(Lazy<IAmazonS3> client, IOptions<MedicalRecordOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (options.Value.StorageProvider != MedicalStorageProvider.S3) return HealthCheckResult.Healthy();
        try
        {
            await client.Value.ListObjectsV2Async(new ListObjectsV2Request { BucketName = options.Value.S3.Bucket, MaxKeys = 1 }, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is AmazonS3Exception or HttpRequestException or IOException)
        {
            return HealthCheckResult.Unhealthy("Medical storage is unavailable.");
        }
    }
}
