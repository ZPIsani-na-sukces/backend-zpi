using Zpi.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Get;

public sealed class GetInvitationHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<InvitationPreviewDto>> Handle(GetInvitationQuery query, CancellationToken cancellationToken)
    {
        var result = await InvitationReader.FindAsync(context, identity, query.Code, query.AccountId, time.GetUtcNow(), cancellationToken);
        if (result.Failure is { } failure) return CareResult<InvitationPreviewDto>.Failed(failure);
        var invitation = result.Value!.Invitation;
        var sender = result.Value.Accounts[invitation.SenderAccountId];
        return CareResult<InvitationPreviewDto>.Success(new(new(sender.FirstName, sender.Role),
            invitation.Status.ToCamelCase(), invitation.CreatedAt, invitation.ExpiresAt));
    }
}
