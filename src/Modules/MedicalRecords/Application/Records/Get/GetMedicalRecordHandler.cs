using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Records.Get;

public sealed class GetMedicalRecordHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalRecordMapper mapper, MedicalAudit audit)
{
    public async Task<MedicalResult<MedicalRecordDto>> Handle(GetMedicalRecordQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        var row = await context.MedicalRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == query.RecordId &&
            row.SeniorId == query.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return MedicalResult<MedicalRecordDto>.Failed(MedicalFailure.ResourceNotFound);
        var result = (await mapper.MapAsync([row], cancellationToken))[0];
        audit.Add(query.AccountId, query.SeniorId, row.Id, null, MedicalActions.View);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<MedicalRecordDto>.Success(result);
    }
}
