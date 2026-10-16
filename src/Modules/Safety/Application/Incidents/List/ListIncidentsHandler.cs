using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Safety.Application.Incidents.List;

public sealed class ListIncidentsHandler(IncidentReader reader, ICareFacade care, IncidentMapper mapper)
{
    public async Task<SafetyResult<PageDto<IncidentDto>>> Handle(ListIncidentsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<PageDto<IncidentDto>>.Failed(SafetyFailure.ResourceNotFound);
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
        var total = await rows
            .CountAsync(cancellationToken);
        var items = await rows
            .OrderByDescending(row => row.ReceivedAt)
            .ThenByDescending(row => row.Id)
            .Skip((request.Page - 1) * request.Limit)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);
        return SafetyResult<PageDto<IncidentDto>>.Success(new PageDto<IncidentDto>(await mapper.MapAsync(items, cancellationToken), request.Page, request.Limit, total));
    }
}
