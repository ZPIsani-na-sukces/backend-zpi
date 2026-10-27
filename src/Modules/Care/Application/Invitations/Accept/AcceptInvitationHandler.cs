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
        var now = time.GetUtcNow();
        var result = await InvitationReader.FindAsync(context, identity, command.Code, command.AccountId, now, cancellationToken);
        if (result.Failure is { } failure) return CareResult<CareRelationshipDto>.Failed(failure);
        var invitation = result.Value!.Invitation;
        var accounts = result.Value.Accounts;
        if (invitation.Status == InvitationStatus.Accepted)
        {
            var existing = await context.SeniorCareRelationships.SingleAsync(row => row.Id == invitation.RelationshipId, cancellationToken);
            return CareResult<CareRelationshipDto>.Success(CareDtoMapper.Relationship(existing, accounts));
        }
        var senderIsSenior = accounts[invitation.SenderAccountId].Role == AccountRoles.Senior;
        var seniorId = senderIsSenior ? invitation.SenderAccountId : command.AccountId;
        var caretakerId = senderIsSenior ? command.AccountId : invitation.SenderAccountId;
        if (await context.SeniorCareRelationships.AnyAsync(row => row.SeniorId == seniorId &&
            row.CaretakerId == caretakerId && row.Status == RelationshipStatus.Active, cancellationToken))
            return CareResult<CareRelationshipDto>.Failed(CareFailure.RelationshipAlreadyExists);
        var respondedAt = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
        var relationship = new SeniorCareRelationship
        {
            SeniorId = seniorId,
            CaretakerId = caretakerId,
            Status = RelationshipStatus.Active,
            CreatedAt = respondedAt
        };
        invitation.Status = InvitationStatus.Accepted;
        invitation.RecipientAccountId = command.AccountId;
        invitation.RelationshipId = relationship.Id;
        invitation.RespondedAt = respondedAt;
        context.SeniorCareRelationships.Add(relationship);
        await context.SaveChangesAsync(cancellationToken);
        return CareResult<CareRelationshipDto>.Success(CareDtoMapper.Relationship(relationship, accounts));
    }
}
