using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Relationships;

public sealed record CareRelationshipDto(Guid RelationshipId, AccountSummary Senior, AccountSummary Caretaker,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? RemovedAt);
