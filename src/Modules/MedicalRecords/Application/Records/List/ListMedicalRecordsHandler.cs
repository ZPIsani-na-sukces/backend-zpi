using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.MedicalRecords.Application.Records.List;

public sealed class ListMedicalRecordsHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalRecordMapper mapper, MedicalAudit audit)
{
    public async Task<MedicalResult<PageDto<MedicalRecordDto>>> Handle(ListMedicalRecordsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return MedicalResult<PageDto<MedicalRecordDto>>.Failed(MedicalFailure.ResourceNotFound);
        var request = query.Request;
        var rows = context.MedicalRecords
            .AsNoTracking()
            .Where(row => row.SeniorId == query.SeniorId && row.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = "%" + request.Search.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_") + "%";
            rows = rows.Where(row => EF.Functions.ILike(row.Title, search, @"\") ||
                row.Description != null && EF.Functions.ILike(row.Description, search, @"\"));
        }
        if (request.RecordType.HasValue) rows = rows.Where(row => row.RecordType == request.RecordType);
        if (request.DocumentDateFrom.HasValue) rows = rows.Where(row => row.DocumentDate >= request.DocumentDateFrom.Value);
        if (request.DocumentDateTo.HasValue) rows = rows.Where(row => row.DocumentDate <= request.DocumentDateTo.Value);
        var total = await rows.CountAsync(cancellationToken);
        var ordered = request.Sort switch
        {
            MedicalRecordSort.DocumentDateAscending => rows.OrderBy(row => row.DocumentDate),
            MedicalRecordSort.CreatedAtDescending => rows.OrderByDescending(row => row.CreatedAt),
            MedicalRecordSort.Title => rows.OrderBy(row => row.Title),
            _ => rows.OrderByDescending(row => row.DocumentDate)
        };
        var page = await ordered
            .ThenBy(row => row.Id)
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);
        var result = new PageDto<MedicalRecordDto>(await mapper.MapAsync(page, cancellationToken), request.Page, request.Limit, total);
        audit.Add(query.AccountId, query.SeniorId, null, null, MedicalActions.List);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<PageDto<MedicalRecordDto>>.Success(result);
    }
}
