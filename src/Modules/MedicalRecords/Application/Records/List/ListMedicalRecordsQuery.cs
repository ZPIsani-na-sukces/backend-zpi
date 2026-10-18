namespace Zpi.Modules.MedicalRecords.Application.Records.List;

public sealed record ListMedicalRecordsQuery(Guid AccountId, Guid SeniorId, ListMedicalRecordsRequest Request);
