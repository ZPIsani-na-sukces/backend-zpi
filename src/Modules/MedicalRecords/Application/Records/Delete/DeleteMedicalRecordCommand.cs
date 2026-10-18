namespace Zpi.Modules.MedicalRecords.Application.Records.Delete;

public sealed record DeleteMedicalRecordCommand(Guid AccountId, Guid SeniorId, Guid RecordId, int Version);
