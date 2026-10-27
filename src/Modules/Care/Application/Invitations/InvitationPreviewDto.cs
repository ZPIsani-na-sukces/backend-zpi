namespace Zpi.Modules.Care.Application.Invitations;

public sealed record InvitationSenderDto(string FirstName, string Role);

public sealed record InvitationPreviewDto(InvitationSenderDto Sender, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
