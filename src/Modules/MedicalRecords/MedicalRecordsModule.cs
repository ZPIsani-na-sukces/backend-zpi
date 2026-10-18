using System.Threading.RateLimiting;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Amazon.S3;
using Microsoft.Extensions.Options;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zpi.Database;
using Zpi.Domain;
using Zpi.Modules.MedicalRecords.Api;
using Zpi.Modules.MedicalRecords.Api.Forms;
using Zpi.Modules.MedicalRecords.Application;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Application.Files.Add;
using Zpi.Modules.MedicalRecords.Application.Files.Content;
using Zpi.Modules.MedicalRecords.Application.Files.Delete;
using Zpi.Modules.MedicalRecords.Application.Records;
using Zpi.Modules.MedicalRecords.Application.Records.Create;
using Zpi.Modules.MedicalRecords.Application.Records.Delete;
using Zpi.Modules.MedicalRecords.Application.Records.Get;
using Zpi.Modules.MedicalRecords.Application.Records.List;
using Zpi.Modules.MedicalRecords.Application.Records.Update;
using Zpi.Modules.MedicalRecords.Infrastructure.BackgroundJobs;
using Zpi.Modules.MedicalRecords.Infrastructure.Storage;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords;

public static class MedicalRecordsModule
{
    public static IServiceCollection AddMedicalRecordsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MedicalRecordOptions>()
            .Bind(configuration.GetSection(MedicalRecordOptions.SectionName))
            .Validate(options => options.MaxFileSizeBytes is > 0 and <= MedicalRecordOptions.MaximumFileSizeBytes && options.MaxFilesPerRecord is >= 1 and <= 10,
                "MedicalRecords file limits must be positive and no greater than 20 MiB / 10 files.")
            .Validate<IHostEnvironment>((options, environment) => !string.IsNullOrWhiteSpace(options.StorageRoot) &&
                !Path.GetFullPath(options.StorageRoot, environment.ContentRootPath).TrimEnd(Path.DirectorySeparatorChar)
                    .StartsWith(Path.Combine(environment.ContentRootPath, "wwwroot"), StringComparison.OrdinalIgnoreCase),
                "MedicalRecords:StorageRoot must be private and outside wwwroot.")
            .Validate(options => Enum.IsDefined(options.StorageProvider), "MedicalRecords:StorageProvider must be Local or S3.")
            .Validate<IHostEnvironment>((options, environment) => options.StorageProvider != MedicalStorageProvider.S3 || ValidS3(options.S3, environment),
                "MedicalRecords:S3 requires HTTPS, a bucket, a region and credentials/CA files. Run docker compose up -d --build for local storage.")
            .ValidateOnStart();
        services.AddControllers().AddApplicationPart(typeof(MedicalRecordsController).Assembly);
        services.AddRateLimiter(options => options.AddPolicy("medical", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })));
        services.AddValidatorsFromAssemblyContaining<CreateMedicalRecordFormValidator>();
        services.AddScoped<MedicalStorageExceptionFilter>();
        services.AddScoped<MedicalAudit>();
        services.AddScoped<LocalMedicalFileStorage>();
        services.AddScoped<S3MedicalFileStorage>();
        services.AddScoped<IMedicalFileStorage, MedicalFileStorage>();
        services.AddSingleton(provider => new Lazy<IAmazonS3>(() => provider.GetRequiredService<IAmazonS3>()));
        services.AddSingleton<IAmazonS3>(provider => S3MedicalClient.Create(
            provider.GetRequiredService<IOptions<MedicalRecordOptions>>().Value.S3, provider.GetRequiredService<IHostEnvironment>()));
        services.AddScoped<MedicalUploadService>();
        services.AddScoped<MedicalFileCleanupProcessor>();
        services.AddScoped<MedicalRecordMapper>();
        services.AddScoped<ListMedicalRecordsHandler>();
        services.AddScoped<GetMedicalRecordHandler>();
        services.AddScoped<CreateMedicalRecordHandler>();
        services.AddScoped<UpdateMedicalRecordHandler>();
        services.AddScoped<DeleteMedicalRecordHandler>();
        services.AddScoped<AddMedicalFileHandler>();
        services.AddScoped<DeleteMedicalFileHandler>();
        services.AddScoped<GetMedicalFileHandler>();
        services.AddScoped<IAccountDataCleanup, AccountDataCleanup>();
        var connectionString = DatabaseConfiguration.GetConnectionString(configuration);
        services.AddDbContext<MedicalRecordsDbContext>(options =>
            DatabaseConfiguration.Configure(options, connectionString, MedicalRecordsDbContext.Schema));
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck<MedicalRecordsDbContext>>("MedicalRecords", tags: ["ready"]);
        services.AddHealthChecks().AddCheck<S3MedicalStorageHealthCheck>("MedicalStorage", tags: ["ready"]);
        if (configuration.GetValue("MedicalRecords:CleanupWorkerEnabled", true)) services.AddHostedService<MedicalFileCleanupWorker>();
        return services;
    }

    private static bool ValidS3(S3MedicalStorageOptions options, IHostEnvironment environment)
    {
        bool HasSecret(string value, string path) => !string.IsNullOrWhiteSpace(value) ||
            (!string.IsNullOrWhiteSpace(path) && File.Exists(Path.GetFullPath(path, environment.ContentRootPath)) &&
                !string.IsNullOrWhiteSpace(File.ReadAllText(Path.GetFullPath(path, environment.ContentRootPath))));
        try
        {
            if (!string.IsNullOrWhiteSpace(options.CertificateAuthorityFile))
            {
                using var certificate = X509Certificate2.CreateFromPem(File.ReadAllText(
                    Path.GetFullPath(options.CertificateAuthorityFile, environment.ContentRootPath)));
            }
            return Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == Uri.UriSchemeHttps &&
                string.IsNullOrEmpty(endpoint.UserInfo) && string.IsNullOrEmpty(endpoint.Query) && string.IsNullOrEmpty(endpoint.Fragment) &&
                !string.IsNullOrWhiteSpace(options.Bucket) && !string.IsNullOrWhiteSpace(options.Region) &&
                HasSecret(options.AccessKey, options.AccessKeyFile) && HasSecret(options.SecretKey, options.SecretKeyFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            return false;
        }
    }
}
