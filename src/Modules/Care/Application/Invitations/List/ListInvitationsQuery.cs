namespace Zpi.Modules.Care.Application.Invitations.List;

public sealed record ListInvitationsQuery(Guid AccountId, string Direction, string? Status, int Page, int Limit);
