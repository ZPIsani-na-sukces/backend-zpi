namespace Zpi.Modules.SeniorProfile.Application.Contacts.List;

public sealed record ListContactsQuery(Guid AccountId, Guid SeniorId, string? Search, bool IsActive,
    string Sort, int Page, int Limit);
