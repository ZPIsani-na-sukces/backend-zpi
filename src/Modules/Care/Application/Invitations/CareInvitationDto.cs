using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations;

public sealed record CareInvitationDto(Guid InvitationId, AccountSummary Sender, AccountSummary Recipient,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RespondedAt);
