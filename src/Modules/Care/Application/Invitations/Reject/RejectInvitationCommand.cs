namespace Zpi.Modules.Care.Application.Invitations.Reject;

public sealed record RejectInvitationCommand(Guid AccountId, Guid InvitationId);
