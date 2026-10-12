using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Delete;

public sealed class DeleteContactHandler(SeniorProfileDbContext context, ICareFacade care, TimeProvider clock)
{
    public async Task<SeniorProfileResult<bool>> Handle(DeleteContactCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SeniorProfileResult<bool>.Failed(SeniorProfileFailure.ResourceNotFound);
        var contact = await context.SeniorContactPersons.SingleOrDefaultAsync(row => row.Id == command.ContactId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (contact is null) return SeniorProfileResult<bool>.Failed(SeniorProfileFailure.ResourceNotFound);
        if (contact.Version != command.ExpectedVersion)
            return SeniorProfileResult<bool>.Failed(SeniorProfileFailure.VersionMismatch);
        contact.IsActive = false;
        contact.DeletedAt = clock.GetUtcNow();
        contact.DeletedByAccountId = command.AccountId;
        contact.UpdatedAt = contact.DeletedAt.Value;
        contact.UpdatedByAccountId = command.AccountId;
        await context.SaveChangesAsync(cancellationToken);
        return SeniorProfileResult<bool>.Success(true);
    }
}
