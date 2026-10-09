namespace Zpi.Modules.Care.Application.Relationships.ListCaretakers;

public sealed record ListCaretakersQuery(Guid AccountId, Guid SeniorId, int Page, int Limit);
