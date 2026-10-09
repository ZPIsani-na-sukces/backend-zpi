using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application;

internal static class CareDtoMapper
{
    public static CareInvitationDto Invitation(SeniorCareInvitation invitation,
        IReadOnlyDictionary<Guid, AccountSummary> accounts, DateTimeOffset now) => new(invitation.Id,
        accounts[invitation.SenderAccountId], accounts[invitation.RecipientAccountId],
        (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now ? InvitationStatus.Expired : invitation.Status).ToCamelCase(),
        invitation.CreatedAt, invitation.ExpiresAt, invitation.RespondedAt);

    public static CareRelationshipDto Relationship(SeniorCareRelationship relationship,
        IReadOnlyDictionary<Guid, AccountSummary> accounts) => new(relationship.Id,
        accounts[relationship.SeniorId], accounts[relationship.CaretakerId], relationship.Status.ToCamelCase(),
        relationship.CreatedAt, relationship.RemovedAt);
}
