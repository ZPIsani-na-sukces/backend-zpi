namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed record ListSeniorsQuery(Guid AccountId, string? Search, string? Cursor, int Limit);
