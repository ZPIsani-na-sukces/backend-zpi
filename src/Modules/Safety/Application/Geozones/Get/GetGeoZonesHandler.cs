using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Geozones.Get;

public sealed class GetGeoZonesHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<GeoZoneDto>> Handle(GetGeoZonesQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ResourceNotFound);
        var rows = context.GeoZones
            .AsNoTracking()
            .Where(row => row.SeniorId == query.SeniorId && row.DeletedAt == null);
        var row = await rows.SingleOrDefaultAsync(row => row.Id == query.ZoneId, cancellationToken);
        return row is null ? SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ResourceNotFound) : SafetyResult<GeoZoneDto>.Success(GeoZoneDto.From(row));
    }
}
