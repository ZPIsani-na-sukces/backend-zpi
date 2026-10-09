using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;

namespace Zpi.Modules.Care.Application.Invitations;

internal static class InvitationReader
{
    public static Task<SeniorCareInvitation?> FindVisibleAsync(CareDbContext context, Guid invitationId,
        Guid accountId, CancellationToken cancellationToken) => context.SeniorCareInvitations
        .SingleOrDefaultAsync(invitation => invitation.Id == invitationId &&
            (invitation.SenderAccountId == accountId || invitation.RecipientAccountId == accountId), cancellationToken);
}
