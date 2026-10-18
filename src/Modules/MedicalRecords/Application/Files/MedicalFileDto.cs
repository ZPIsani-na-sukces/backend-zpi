using Zpi.Modules.MedicalRecords.Domain;

namespace Zpi.Modules.MedicalRecords.Application.Files;

public sealed record MedicalFileDto(Guid FileId, string OriginalName, string MimeType, long SizeBytes, DateTimeOffset CreatedAt)
{
    public static MedicalFileDto From(MedicalFile file) => new(file.Id, file.OriginalName, file.MimeType, file.SizeBytes, file.CreatedAt);
}
