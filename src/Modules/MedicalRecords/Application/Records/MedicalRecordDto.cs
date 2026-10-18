using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Application.Records;

public sealed record MedicalRecordDto(Guid RecordId, Guid SeniorId, string Title, string? Description, string RecordType,
    DateOnly DocumentDate, IReadOnlyList<MedicalFileDto> Files, AccountSummary? CreatedBy, DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, int Version);
