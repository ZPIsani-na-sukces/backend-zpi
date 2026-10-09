using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Accept;

public sealed class AcceptInvitationHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<CareRelationshipDto>> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await InvitationReader.FindVisibleAsync(context, command.InvitationId, command.AccountId, cancellationToken);
        if (invitation is null) return CareResult<CareRelationshipDto>.Failed(CareFailure.ResourceNotFound);
        if (invitation.RecipientAccountId != command.AccountId)
            return CareResult<CareRelationshipDto>.Failed(CareFailure.OperationForbidden);
        var accounts = await identity.GetActiveAccountsAsync([invitation.SeniorId, invitation.CaretakerId], cancellationToken);
        if (accounts.Count != 2) return CareResult<CareRelationshipDto>.Failed(CareFailure.ResourceNotFound);
        if (invitation.Status == InvitationStatus.Accepted)
        {
            var existing = await context.SeniorCareRelationships.SingleAsync(relationship => relationship.InvitationId == invitation.Id, cancellationToken);
            return CareResult<CareRelationshipDto>.Success(CareDtoMapper.Relationship(existing, accounts));
        }
        var now = time.GetUtcNow();
        if (invitation.Status == InvitationStatus.Expired || invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            invitation.Status = InvitationStatus.Expired;
            await context.SaveChangesAsync(cancellationToken);
            return CareResult<CareRelationshipDto>.Failed(CareFailure.InvitationExpired);
        }
        if (invitation.Status != InvitationStatus.Pending)
            return CareResult<CareRelationshipDto>.Failed(CareFailure.InvalidState);
        if (await context.SeniorCareRelationships.AnyAsync(relationship => relationship.SeniorId == invitation.SeniorId &&
            relationship.CaretakerId == invitation.CaretakerId && relationship.Status == RelationshipStatus.Active, cancellationToken))
            return CareResult<CareRelationshipDto>.Failed(CareFailure.RelationshipAlreadyExists);
        var relationship = new SeniorCareRelationship
        {
            InvitationId = invitation.Id,
            SeniorId = invitation.SeniorId,
            CaretakerId = invitation.CaretakerId,
            Status = RelationshipStatus.Active,
            CreatedAt = now
        };
        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = now;
        context.SeniorCareRelationships.Add(relationship);
        await context.SaveChangesAsync(cancellationToken);
        return CareResult<CareRelationshipDto>.Success(CareDtoMapper.Relationship(relationship, accounts));
    }
}
