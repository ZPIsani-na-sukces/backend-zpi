namespace Zpi.Modules.Care.Application.Invitations.Create;

public sealed record CreateInvitationCommand(Guid AccountId, string RecipientIdentifier);
