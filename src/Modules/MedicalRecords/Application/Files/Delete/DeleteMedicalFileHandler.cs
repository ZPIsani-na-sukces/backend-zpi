using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Files.Delete;

public sealed class DeleteMedicalFileHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalAudit audit, TimeProvider clock)
{
    public async Task<MedicalResult<int>> Handle(DeleteMedicalFileCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<int>.Failed(MedicalFailure.ResourceNotFound);
        var row = await context.MedicalRecords.SingleOrDefaultAsync(row => row.Id == command.RecordId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return MedicalResult<int>.Failed(MedicalFailure.ResourceNotFound);
        if (row.Version != command.Version) return MedicalResult<int>.Failed(MedicalFailure.VersionMismatch);
        var file = await context.MedicalFiles.SingleOrDefaultAsync(file => file.Id == command.FileId && file.MedicalRecordId == row.Id && file.DeletedAt == null, cancellationToken);
        if (file is null) return MedicalResult<int>.Failed(MedicalFailure.ResourceNotFound);
        var now = clock.GetUtcNow();
        file.DeletedAt = now;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        context.Entry(row).Property(record => record.UpdatedAt).IsModified = true;
        audit.Add(command.AccountId, command.SeniorId, row.Id, file.Id, MedicalActions.DeleteFile);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<int>.Success(row.Version);
    }
}
