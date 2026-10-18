namespace Zpi.Modules.MedicalRecords.Application.Files.Delete;

public sealed record DeleteMedicalFileCommand(Guid AccountId, Guid SeniorId, Guid RecordId, Guid FileId, int Version);
