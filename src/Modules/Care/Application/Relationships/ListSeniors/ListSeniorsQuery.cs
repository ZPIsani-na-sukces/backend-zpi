namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed record ListSeniorsQuery(Guid AccountId, string? Search, int Page, int Limit);
