using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Files.Add;

public sealed class AddMedicalFileHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalUploadService uploads,
    IOptions<MedicalRecordOptions> options, MedicalAudit audit, TimeProvider clock)
{
    public async Task<MedicalResult<AddedMedicalFileDto>> Handle(AddMedicalFileCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.ResourceNotFound);
        var parent = await context.MedicalRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == command.RecordId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (parent is null) return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.ResourceNotFound);
        if (parent.Version != command.Version) return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.VersionMismatch);
        if (await context.MedicalFiles.CountAsync(row => row.MedicalRecordId == parent.Id && row.DeletedAt == null, cancellationToken) >= options.Value.MaxFilesPerRecord)
            return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.FileLimitReached);
        var saved = (await uploads.SaveAsync([command.File], cancellationToken))[0];
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.ResourceNotFound);
        var row = await context.MedicalRecords.SingleOrDefaultAsync(row => row.Id == command.RecordId && row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.ResourceNotFound);
        if (row.Version != command.Version) return MedicalResult<AddedMedicalFileDto>.Failed(MedicalFailure.VersionMismatch);
        var now = clock.GetUtcNow();
        var file = new MedicalFile
        {
            MedicalRecordId = row.Id,
            ObjectKey = saved.ObjectKey,
            OriginalName = saved.OriginalName,
            MimeType = saved.MimeType,
            SizeBytes = saved.SizeBytes,
            ChecksumSha256 = saved.ChecksumSha256,
            CreatedAt = now,
            CreatedByAccountId = command.AccountId
        };
        context.MedicalFiles.Add(file);
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        context.Entry(row).Property(record => record.UpdatedAt).IsModified = true;
        audit.Add(command.AccountId, command.SeniorId, row.Id, file.Id, MedicalActions.AddFile);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<AddedMedicalFileDto>.Success(new(MedicalFileDto.From(file), row.Version));
    }
}
