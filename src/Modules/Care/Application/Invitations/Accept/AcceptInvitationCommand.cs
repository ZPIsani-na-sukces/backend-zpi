namespace Zpi.Modules.Care.Application.Invitations.Accept;

public sealed record AcceptInvitationCommand(Guid AccountId, string Code);
