using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Reject;

public sealed class RejectInvitationHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<CareInvitationDto>> Handle(RejectInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await InvitationReader.FindVisibleAsync(context, command.InvitationId, command.AccountId, cancellationToken);
        if (invitation is null) return CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound);
        if (invitation.RecipientAccountId != command.AccountId)
            return CareResult<CareInvitationDto>.Failed(CareFailure.OperationForbidden);
        var accounts = await identity.GetActiveAccountsAsync([invitation.SeniorId, invitation.CaretakerId], cancellationToken);
        if (accounts.Count != 2) return CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound);
        var now = time.GetUtcNow();
        if (invitation.Status == InvitationStatus.Rejected)
            return CareResult<CareInvitationDto>.Success(CareDtoMapper.Invitation(invitation, accounts, now));
        if (invitation.Status == InvitationStatus.Expired || invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now)
        {
            invitation.Status = InvitationStatus.Expired;
            await context.SaveChangesAsync(cancellationToken);
            return CareResult<CareInvitationDto>.Failed(CareFailure.InvitationExpired);
        }
        if (invitation.Status != InvitationStatus.Pending)
            return CareResult<CareInvitationDto>.Failed(CareFailure.InvalidState);
        invitation.Status = InvitationStatus.Rejected;
        invitation.RespondedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return CareResult<CareInvitationDto>.Success(CareDtoMapper.Invitation(invitation, accounts, now));
    }
}
