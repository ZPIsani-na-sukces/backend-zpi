using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations;

internal static class InvitationReader
{
    public static async Task<CareResult<InvitationAccess>> FindAsync(CareDbContext context, IIdentityFacade identity,
        string code, Guid accountId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var invitation = await context.SeniorCareInvitations
            .SingleOrDefaultAsync(row => row.Code == code && row.ExpiresAt > now, cancellationToken);
        if (invitation is null) return CareResult<InvitationAccess>.Failed(CareFailure.ResourceNotFound);
        var accounts = await identity.GetActiveAccountsAsync([invitation.SenderAccountId, accountId], cancellationToken);
        if (!accounts.ContainsKey(invitation.SenderAccountId) || !accounts.ContainsKey(accountId))
            return CareResult<InvitationAccess>.Failed(CareFailure.ResourceNotFound);
        var sender = accounts[invitation.SenderAccountId];
        var recipient = accounts[accountId];
        if (invitation.SenderAccountId == accountId ||
            !(sender.Role == AccountRoles.Senior && recipient.Role == AccountRoles.Caretaker ||
              sender.Role == AccountRoles.Caretaker && recipient.Role == AccountRoles.Senior))
            return CareResult<InvitationAccess>.Failed(CareFailure.OperationForbidden);
        if (invitation.Status == InvitationStatus.Accepted && invitation.RecipientAccountId != accountId)
            return CareResult<InvitationAccess>.Failed(CareFailure.InvalidState);
        return CareResult<InvitationAccess>.Success(new(invitation, accounts));
    }
}

internal sealed record InvitationAccess(SeniorCareInvitation Invitation, IReadOnlyDictionary<Guid, AccountSummary> Accounts);
