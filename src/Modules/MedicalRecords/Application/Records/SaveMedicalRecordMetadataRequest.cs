using Zpi.Modules.MedicalRecords.Domain;

namespace Zpi.Modules.MedicalRecords.Application.Records;

public sealed record SaveMedicalRecordMetadataRequest(string Title, string? Description, MedicalRecordType RecordType, DateOnly DocumentDate);
