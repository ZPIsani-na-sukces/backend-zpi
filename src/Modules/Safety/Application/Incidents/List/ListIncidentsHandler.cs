using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Safety.Application.Incidents.List;

public sealed class ListIncidentsHandler(IncidentReader reader, ICareFacade care, IncidentMapper mapper)
{
    public async Task<SafetyResult<CursorPageDto<IncidentDto>>> Handle(ListIncidentsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<CursorPageDto<IncidentDto>>.Failed(SafetyFailure.ResourceNotFound);
        var request = query.Request;
        var rows = reader.Query(query.SeniorId);
        if (request.Type.HasValue) rows = rows.Where(row => row.Type == request.Type);
        if (request.Status.HasValue) rows = rows
            .Where(row => row.Status == request.Status);
        if (request.From.HasValue)
        {
            var from = request.From.Value.ToUniversalTime(); rows = rows
            .Where(row => row.ReceivedAt >= from);
        }
        if (request.To.HasValue)
        {
            var to = request.To.Value.ToUniversalTime(); rows = rows
            .Where(row => row.ReceivedAt < to);
        }
        var pagination = rows.Paginate(request.Cursor, new { query.AccountId, query.SeniorId, request.Type,
            request.Status, request.From, request.To }).OrderBy(row => row.ReceivedAt, true).OrderBy(row => row.Id, true);
        var items = await pagination.Take(request.Limit).ToListAsync(cancellationToken);
        return SafetyResult<CursorPageDto<IncidentDto>>.Success(new CursorPageDto<IncidentDto>(
            await mapper.MapAsync(items.Take(request.Limit).ToList(), cancellationToken), pagination.NextCursor(items, request.Limit)));
    }
}
