using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Get;

public sealed class GetInvitationHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<CareInvitationDto>> Handle(GetInvitationQuery query, CancellationToken cancellationToken)
    {
        var invitation = await InvitationReader.FindVisibleAsync(context, query.InvitationId, query.AccountId, cancellationToken);
        if (invitation is null) return CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound);
        var accounts = await identity.GetActiveAccountsAsync([invitation.SeniorId, invitation.CaretakerId], cancellationToken);
        return accounts.Count != 2 ? CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound)
            : CareResult<CareInvitationDto>.Success(CareDtoMapper.Invitation(invitation, accounts, time.GetUtcNow()));
    }
}
