namespace Zpi.Modules.MedicalRecords.Application.Files.Add;

public sealed record AddMedicalFileCommand(Guid AccountId, Guid SeniorId, Guid RecordId, int Version, MedicalUploadFile File);
public sealed record AddedMedicalFileDto(MedicalFileDto File, int Version);
