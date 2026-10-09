using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;

namespace Zpi.Modules.Care.Application.Relationships.Remove;

public sealed class RemoveRelationshipHandler(CareDbContext context, TimeProvider time)
{
    public async Task<CareResult<bool>> Handle(RemoveRelationshipCommand command, CancellationToken cancellationToken)
    {
        var relationship = await context.SeniorCareRelationships.SingleOrDefaultAsync(row => row.Id == command.RelationshipId &&
            (row.SeniorId == command.AccountId || row.CaretakerId == command.AccountId), cancellationToken);
        if (relationship is null) return CareResult<bool>.Failed(CareFailure.ResourceNotFound);
        if (relationship.Status == RelationshipStatus.Removed) return CareResult<bool>.Success(true);
        relationship.Status = RelationshipStatus.Removed;
        relationship.RemovedAt = time.GetUtcNow();
        relationship.RemovedByAccountId = command.AccountId;
        await context.SaveChangesAsync(cancellationToken);
        return CareResult<bool>.Success(true);
    }
}
