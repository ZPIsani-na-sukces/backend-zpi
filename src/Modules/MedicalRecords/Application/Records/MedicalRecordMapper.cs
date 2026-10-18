using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.MedicalRecords.Application.Files;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Records;

public sealed class MedicalRecordMapper(MedicalRecordsDbContext context, IIdentityFacade identity)
{
    public async Task<IReadOnlyList<MedicalRecordDto>> MapAsync(IReadOnlyList<MedicalRecord> records, CancellationToken cancellationToken)
    {
        var ids = records.Select(row => row.Id).ToArray();
        var files = (await context.MedicalFiles
            .AsNoTracking()
            .Where(row => ids.Contains(row.MedicalRecordId) && row.DeletedAt == null)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .ToListAsync(cancellationToken)).ToLookup(row => row.MedicalRecordId);
        var creators = await identity.GetAccountSummariesAsync(records.Select(row => row.CreatedByAccountId).Distinct().ToArray(), cancellationToken);
        return records.Select(row => new MedicalRecordDto(row.Id, row.SeniorId, row.Title, row.Description, row.RecordType.ToCamelCase(),
            row.DocumentDate, files[row.Id].Select(MedicalFileDto.From).ToArray(), creators.GetValueOrDefault(row.CreatedByAccountId),
            row.CreatedAt, row.UpdatedAt, row.Version)).ToArray();
    }
}
