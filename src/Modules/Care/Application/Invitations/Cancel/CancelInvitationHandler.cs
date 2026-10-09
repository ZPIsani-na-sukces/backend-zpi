using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;

namespace Zpi.Modules.Care.Application.Invitations.Cancel;

public sealed class CancelInvitationHandler(CareDbContext context, TimeProvider time)
{
    public async Task<CareResult<bool>> Handle(CancelInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await InvitationReader.FindVisibleAsync(context, command.InvitationId, command.AccountId, cancellationToken);
        if (invitation is null) return CareResult<bool>.Failed(CareFailure.ResourceNotFound);
        if (invitation.SenderAccountId != command.AccountId) return CareResult<bool>.Failed(CareFailure.OperationForbidden);
        if (invitation.Status == InvitationStatus.Cancelled) return CareResult<bool>.Success(true);
        var now = time.GetUtcNow();
        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt <= now)
            return CareResult<bool>.Failed(CareFailure.InvalidState);
        invitation.Status = InvitationStatus.Cancelled;
        invitation.RespondedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        return CareResult<bool>.Success(true);
    }
}
