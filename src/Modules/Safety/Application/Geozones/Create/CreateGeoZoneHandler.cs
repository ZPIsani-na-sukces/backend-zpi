using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Geozones.Create;

public sealed class CreateGeoZoneHandler(SafetyDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SafetyResult<GeoZoneDto>> Handle(CreateGeoZoneCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = clock.GetUtcNow();
        if (await context.GeoZones.CountAsync(row => row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken) >= 20)
            return SafetyResult<GeoZoneDto>.Failed(SafetyFailure.ZoneLimitReached);
        var row = new GeoZone
        {
            SeniorId = command.SeniorId,
            Name = command.Request.Name.Trim(),
            CreatedAt = now,
            CreatedByAccountId = command.AccountId
        };
        context.GeoZones.Add(row);
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
