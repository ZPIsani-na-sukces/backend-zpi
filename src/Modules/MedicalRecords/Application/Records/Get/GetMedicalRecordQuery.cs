namespace Zpi.Modules.MedicalRecords.Application.Records.Get;

public sealed record GetMedicalRecordQuery(Guid AccountId, Guid SeniorId, Guid RecordId);
