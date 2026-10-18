using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Records.Update;

public sealed class UpdateMedicalRecordHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalRecordMapper mapper, MedicalAudit audit, TimeProvider clock)
{
    public async Task<MedicalResult<MedicalRecordDto>> Handle(UpdateMedicalRecordCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        var row = await context.MedicalRecords.SingleOrDefaultAsync(row => row.Id == command.RecordId && row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        if (row.Version != command.Version) return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.VersionMismatch);
        var now = clock.GetUtcNow();
        var request = command.Metadata;
        row.Title = request.Title.Trim();
        row.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        row.RecordType = request.RecordType;
        row.DocumentDate = request.DocumentDate;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        context.Entry(row).Property(record => record.UpdatedAt).IsModified = true;
        audit.Add(command.AccountId, command.SeniorId, row.Id, null, MedicalActions.Update);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<MedicalRecordDto>.Success((await mapper.MapAsync([row], cancellationToken))[0]);
    }
}
