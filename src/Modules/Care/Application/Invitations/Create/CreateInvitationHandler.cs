using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.Create;

public sealed class CreateInvitationHandler(CareDbContext context, IIdentityFacade identity,
    IInvitationCodeGenerator codes, TimeProvider time)
{
    public async Task<CareResult<CreateInvitationResult>> Handle(CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        var accounts = await identity.GetActiveAccountsAsync([command.AccountId], cancellationToken);
        if (!accounts.TryGetValue(command.AccountId, out var sender))
            return CareResult<CreateInvitationResult>.Failed(CareFailure.ResourceNotFound);
        if (sender.Role is not (AccountRoles.Senior or AccountRoles.Caretaker))
            return CareResult<CreateInvitationResult>.Failed(CareFailure.OperationForbidden);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.FromUnixTimeMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
        await context.SeniorCareInvitations
            .Where(row => row.SenderAccountId == command.AccountId && row.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        var existing = await context.SeniorCareInvitations.AsNoTracking()
            .SingleOrDefaultAsync(row => row.SenderAccountId == command.AccountId && row.Status == InvitationStatus.Pending, cancellationToken);
        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return CareResult<CreateInvitationResult>.Success(new(CareDtoMapper.Invitation(existing), false));
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var invitation = new SeniorCareInvitation
            {
                SenderAccountId = command.AccountId,
                Code = codes.Generate(),
                Status = InvitationStatus.Pending,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(15)
            };
            context.SeniorCareInvitations.Add(invitation);
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                context.Entry(invitation).State = EntityState.Detached;
                if (await context.SeniorCareInvitations.AnyAsync(row => row.Code == invitation.Code, cancellationToken))
                    continue;
                throw;
            }
            await transaction.CommitAsync(cancellationToken);
            return CareResult<CreateInvitationResult>.Success(new(CareDtoMapper.Invitation(invitation), true));
        }
        return CareResult<CreateInvitationResult>.Failed(CareFailure.InvalidState);
    }
}
