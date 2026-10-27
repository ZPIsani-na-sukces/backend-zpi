namespace Zpi.Modules.Care.Application.Invitations;

public sealed record CareInvitationDto(string Code, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);
