using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Create;

public sealed class CreateInvitationHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<CareInvitationDto>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var senior = await identity.FindActiveSeniorAsync(command.RecipientIdentifier, cancellationToken);
        if (senior is null || senior.UserId == command.AccountId)
            return CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync([senior.UserId, command.AccountId], cancellationToken);
        if (accounts.Count != 2) return CareResult<CareInvitationDto>.Failed(CareFailure.ResourceNotFound);
        if (accounts[command.AccountId].Role != "caretaker")
            return CareResult<CareInvitationDto>.Failed(CareFailure.OperationForbidden);
        if (await context.SeniorCareRelationships.AnyAsync(relationship => relationship.SeniorId == senior.UserId &&
            relationship.CaretakerId == command.AccountId && relationship.Status == RelationshipStatus.Active, cancellationToken))
            return CareResult<CareInvitationDto>.Failed(CareFailure.RelationshipAlreadyExists);

        var now = time.GetUtcNow();
        var invitation = await context.SeniorCareInvitations.SingleOrDefaultAsync(row => row.SeniorId == senior.UserId &&
            row.CaretakerId == command.AccountId && row.Status == InvitationStatus.Pending, cancellationToken);
        if (invitation is not null && invitation.ExpiresAt > now)
            return CareResult<CareInvitationDto>.Success(CareDtoMapper.Invitation(invitation, accounts, now));
        if (invitation is not null)
        {
            invitation.Status = InvitationStatus.Expired;
            await context.SaveChangesAsync(cancellationToken);
        }
        invitation = new SeniorCareInvitation
        {
            SeniorId = senior.UserId,
            CaretakerId = command.AccountId,
            SenderAccountId = command.AccountId,
            RecipientAccountId = senior.UserId,
            Status = InvitationStatus.Pending,
            CreatedAt = now,
            ExpiresAt = now.AddDays(7)
        };
        context.SeniorCareInvitations.Add(invitation);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return CareResult<CareInvitationDto>.Success(CareDtoMapper.Invitation(invitation, accounts, now));
    }
}
