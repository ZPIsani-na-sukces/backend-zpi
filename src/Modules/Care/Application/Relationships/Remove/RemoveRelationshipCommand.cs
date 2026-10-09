namespace Zpi.Modules.Care.Application.Relationships.Remove;

public sealed record RemoveRelationshipCommand(Guid AccountId, Guid RelationshipId);
