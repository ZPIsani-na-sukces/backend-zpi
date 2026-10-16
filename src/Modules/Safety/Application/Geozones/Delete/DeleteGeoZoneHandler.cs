using System.Data;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Safety.Domain;
using Zpi.Modules.Safety.Infrastructure.Persistence;

namespace Zpi.Modules.Safety.Application.Geozones.Delete;

public sealed class DeleteGeoZoneHandler(SafetyDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SafetyResult<bool>> Handle(DeleteGeoZoneCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SafetyResult<bool>.Failed(SafetyFailure.ResourceNotFound);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var now = clock.GetUtcNow();
        var row = await context.GeoZones.SingleOrDefaultAsync(row => row.Id == command.ZoneId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return SafetyResult<bool>.Failed(SafetyFailure.ResourceNotFound);
        if (row.Version != command.ExpectedVersion) return SafetyResult<bool>.Failed(SafetyFailure.VersionMismatch);
        row.DeletedAt = now;
        row.DeletedByAccountId = command.AccountId;
        row.IsActive = false;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        var state = await context.AutomaticAlertStates.SingleOrDefaultAsync(row => row.SeniorId == command.SeniorId, cancellationToken);
        state?.ResetGeofence();
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SafetyResult<bool>.Success(true);
    }
}
