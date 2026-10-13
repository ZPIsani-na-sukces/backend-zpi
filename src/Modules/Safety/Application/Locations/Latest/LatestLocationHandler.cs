using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Locations.Latest;

public sealed class LatestLocationHandler(SafetyDbContext context, ICareFacade care)
{
    public async Task<SafetyResult<LatestLocationDto>> Handle(LatestLocationQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SafetyResult<LatestLocationDto>.Failed(SafetyFailure.ResourceNotFound);

        var devices = context.Devices.Where(row => row.AssignedAccountId == query.SeniorId).Select(row => row.Id);
        var location = await context.LocationSamples.AsNoTracking()
            .Where(row => devices.Contains(row.DeviceId))
            .OrderByDescending(row => row.CapturedAt)
            .ThenByDescending(row => row.ReceivedAt)
            .ThenByDescending(row => row.Id)
            .Select(row => new LatestLocationDto(row.DeviceId, new CoordinatesDto(row.Latitude, row.Longitude),
                row.AccuracyMeters, row.SpeedMps, row.CapturedAt, row.ReceivedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return location is null ? SafetyResult<LatestLocationDto>.Failed(SafetyFailure.ResourceNotFound)
            : SafetyResult<LatestLocationDto>.Success(location);
    }
}
