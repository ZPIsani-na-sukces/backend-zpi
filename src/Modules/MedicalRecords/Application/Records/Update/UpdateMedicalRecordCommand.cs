namespace Zpi.Modules.MedicalRecords.Application.Records.Update;

public sealed record UpdateMedicalRecordCommand(Guid AccountId, Guid SeniorId, Guid RecordId, int Version, SaveMedicalRecordMetadataRequest Metadata);
