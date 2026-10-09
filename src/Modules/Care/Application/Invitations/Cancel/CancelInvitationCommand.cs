namespace Zpi.Modules.Care.Application.Invitations.Cancel;

public sealed record CancelInvitationCommand(Guid AccountId, Guid InvitationId);
