namespace Zpi.Modules.MedicalRecords.Application.Files.Content;

public sealed record GetMedicalFileQuery(Guid AccountId, Guid SeniorId, Guid RecordId, Guid FileId);
public sealed record MedicalFileContent(Stream Content, string MimeType, string OriginalName);
