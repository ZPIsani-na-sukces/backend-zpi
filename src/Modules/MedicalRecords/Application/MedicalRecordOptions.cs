namespace Zpi.Modules.MedicalRecords.Application;

public sealed class MedicalRecordOptions
{
    public const long MaximumFileSizeBytes = 20 * 1024 * 1024;
    public const string SectionName = "MedicalRecords";
    public MedicalStorageProvider StorageProvider { get; set; } = MedicalStorageProvider.Local;
    public S3MedicalStorageOptions S3 { get; set; } = new();
    public string StorageRoot { get; set; } = "App_Data/medical-records";
    public long MaxFileSizeBytes { get; set; } = MaximumFileSizeBytes;
    public int MaxFilesPerRecord { get; set; } = 10;
    public bool CleanupWorkerEnabled { get; set; } = true;
}

public sealed class S3MedicalStorageOptions
{
    public string Endpoint { get; set; } = "https://localhost:8333";
    public string Bucket { get; set; } = "zpi-medical";
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string AccessKeyFile { get; set; } = "../../.local/seaweedfs/client/access-key";
    public string SecretKeyFile { get; set; } = "../../.local/seaweedfs/client/secret-key";
    public string CertificateAuthorityFile { get; set; } = "../../.local/seaweedfs/client/ca.crt";
}

public enum MedicalStorageProvider { Local, S3 }
