using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.MedicalRecords.Application.Records.List;

public sealed class ListMedicalRecordsHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalRecordMapper mapper, MedicalAudit audit)
{
    public async Task<MedicalResult<CursorPageDto<MedicalRecordDto>>> Handle(ListMedicalRecordsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return MedicalResult<CursorPageDto<MedicalRecordDto>>.Failed(MedicalFailure.ResourceNotFound);
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
        var pagination = rows.Paginate(request.Cursor, new { query.AccountId, query.SeniorId, request.Search,
            request.RecordType, request.DocumentDateFrom, request.DocumentDateTo, request.Sort });
        switch (request.Sort)
        {
            case MedicalRecordSort.DocumentDateAscending: pagination.OrderBy(row => row.DocumentDate); break;
            case MedicalRecordSort.CreatedAtDescending: pagination.OrderBy(row => row.CreatedAt, true); break;
            case MedicalRecordSort.Title: pagination.OrderBy(row => row.Title); break;
            default: pagination.OrderBy(row => row.DocumentDate, true); break;
        }
        pagination.OrderBy(row => row.Id);
        var page = await pagination.Take(request.Limit).ToListAsync(cancellationToken);
        var result = new CursorPageDto<MedicalRecordDto>(await mapper.MapAsync(page.Take(request.Limit).ToList(), cancellationToken),
            pagination.NextCursor(page, request.Limit));
        audit.Add(query.AccountId, query.SeniorId, null, null, MedicalActions.List);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<CursorPageDto<MedicalRecordDto>>.Success(result);
    }
}
