using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Zpi.Modules.MedicalRecords.Application;

namespace Zpi.Modules.MedicalRecords.Infrastructure.Storage;

internal static class S3MedicalClient
{
    public static IAmazonS3 Create(S3MedicalStorageOptions options, IHostEnvironment environment)
    {
        var accessKey = Secret(options.AccessKey, options.AccessKeyFile, environment);
        var secretKey = Secret(options.SecretKey, options.SecretKeyFile, environment);
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            AuthenticationRegion = options.Region,
            ForcePathStyle = true,
            Timeout = TimeSpan.FromSeconds(30)
        };
        if (!string.IsNullOrWhiteSpace(options.CertificateAuthorityFile))
            config.HttpClientFactory = new StorageHttpClientFactory(Path.GetFullPath(options.CertificateAuthorityFile, environment.ContentRootPath));
        return new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), config);
    }

    private static string Secret(string value, string path, IHostEnvironment environment) =>
        !string.IsNullOrWhiteSpace(value) ? value : File.ReadAllText(Path.GetFullPath(path, environment.ContentRootPath)).Trim();

    private sealed class StorageHttpClientFactory(string certificateAuthorityFile) : HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
        {
            var policy = new X509ChainPolicy
            {
                TrustMode = X509ChainTrustMode.CustomRootTrust,
                RevocationMode = X509RevocationMode.NoCheck
            };
            policy.CustomTrustStore.Add(X509Certificate2.CreateFromPem(File.ReadAllText(certificateAuthorityFile)));
            policy.ApplicationPolicy.Add(new System.Security.Cryptography.Oid("1.3.6.1.5.5.7.3.1"));
            return new HttpClient(new SocketsHttpHandler
            {
                SslOptions = new SslClientAuthenticationOptions { CertificateChainPolicy = policy },
                ConnectTimeout = TimeSpan.FromSeconds(5),
                AllowAutoRedirect = false
            });
        }
    }
}
