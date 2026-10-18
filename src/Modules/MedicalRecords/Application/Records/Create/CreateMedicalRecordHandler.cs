using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Records.Create;

public sealed class CreateMedicalRecordHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalRecordMapper mapper,
    MedicalUploadService uploads, MedicalAudit audit, TimeProvider clock)
{
    public async Task<MedicalResult<MedicalRecordDto>> Handle(CreateMedicalRecordCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        var files = await uploads.SaveAsync(command.Files, cancellationToken);
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        var now = clock.GetUtcNow();
        var request = command.Metadata;
        var record = new MedicalRecord
        {
            SeniorId = command.SeniorId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            RecordType = request.RecordType,
            DocumentDate = request.DocumentDate,
            CreatedByAccountId = command.AccountId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.MedicalRecords.Add(record);
        foreach (var file in files) context.MedicalFiles.Add(new MedicalFile
        {
            MedicalRecordId = record.Id,
            ObjectKey = file.ObjectKey,
            OriginalName = file.OriginalName,
            MimeType = file.MimeType,
            SizeBytes = file.SizeBytes,
            ChecksumSha256 = file.ChecksumSha256,
            CreatedByAccountId = command.AccountId,
            CreatedAt = now
        });
        audit.Add(command.AccountId, command.SeniorId, record.Id, null, MedicalActions.Create);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<MedicalRecordDto>.Success((await mapper.MapAsync([record], cancellationToken))[0]);
    }
}
