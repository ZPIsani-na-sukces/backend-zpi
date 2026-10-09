using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed record SeniorSummaryDto(AccountSummary Senior, Guid RelationshipId);
