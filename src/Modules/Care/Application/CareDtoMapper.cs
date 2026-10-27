using Zpi.Domain;
using Zpi.Modules.Care.Application.Invitations;
using Zpi.Modules.Care.Application.Relationships;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application;

internal static class CareDtoMapper
{
    public static CareInvitationDto Invitation(SeniorCareInvitation invitation) => new(invitation.Code,
        invitation.Status.ToCamelCase(), invitation.CreatedAt, invitation.ExpiresAt);

    public static CareRelationshipDto Relationship(SeniorCareRelationship relationship,
        IReadOnlyDictionary<Guid, AccountSummary> accounts) => new(relationship.Id,
        accounts[relationship.SeniorId], accounts[relationship.CaretakerId], relationship.Status.ToCamelCase(),
        relationship.CreatedAt, relationship.RemovedAt);
}
