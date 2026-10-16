using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Geozones.List;

public sealed class ListGeoZonesHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<IReadOnlyList<GeoZoneDto>>> Handle(ListGeoZonesQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<IReadOnlyList<GeoZoneDto>>.Failed(SafetyFailure.ResourceNotFound);
        var rows = context.GeoZones
            .AsNoTracking()
            .Where(row => row.SeniorId == query.SeniorId && row.DeletedAt == null);
        if (query.IsActive.HasValue) rows = rows.Where(row => row.IsActive == query.IsActive);
        var items = await rows.OrderBy(row => row.Name).ThenBy(row => row.Id).ToListAsync(cancellationToken);
        return SafetyResult<IReadOnlyList<GeoZoneDto>>.Success(items.Select(GeoZoneDto.From).ToArray());
    }
}
