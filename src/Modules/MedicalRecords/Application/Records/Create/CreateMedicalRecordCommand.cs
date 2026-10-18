using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Application.Records.Create;

public sealed record CreateMedicalRecordCommand(Guid AccountId, Guid SeniorId, SaveMedicalRecordMetadataRequest Metadata,
    IReadOnlyList<MedicalUploadFile> Files);
