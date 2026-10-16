using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Geozones.Update;

public sealed class UpdateGeoZoneHandler(SafetyDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SafetyResult<GeoZoneDto>> Handle(UpdateGeoZoneCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = clock.GetUtcNow();
        var row = await context.GeoZones.SingleOrDefaultAsync(row => row.Id == command.ZoneId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ResourceNotFound);
        if (row.Version != command.ExpectedVersion) return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.VersionMismatch);
        var request = command.Request;
        row.Name = request.Name.Trim();
        row.CenterLatitude = request.Center.Latitude;
        row.CenterLongitude = request.Center.Longitude;
        row.RadiusMeters = request.RadiusMeters;
        row.IsActive = request.IsActive;
        row.SmsOnExitEnabled = request.SmsOnExitEnabled;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        var state = await context.AutomaticAlertStates.SingleOrDefaultAsync(row => row.SeniorId == command.SeniorId, cancellationToken);
        state?.ResetGeofence();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SafetyResult<GeoZoneDto>.Success(GeoZoneDto.From(row));
    }
}
