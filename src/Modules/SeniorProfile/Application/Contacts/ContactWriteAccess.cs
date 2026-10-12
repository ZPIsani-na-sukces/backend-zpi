using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts;

public sealed class ContactWriteAccess(ICareFacade care, SeniorProfileDbContext context)
{
    public async Task<SeniorProfileFailure?> CheckAsync(Guid accountId, Guid seniorId, CancellationToken cancellationToken)
    {
        if (accountId != seniorId)
            return await care.HasActiveRelationshipAsync(accountId, seniorId, cancellationToken)
                ? null : SeniorProfileFailure.ResourceNotFound;
        if (!await care.CanAccessSeniorAsync(accountId, seniorId, cancellationToken))
            return SeniorProfileFailure.ResourceNotFound;
        return await context.ContactSettings.AsNoTracking().AnyAsync(settings =>
            settings.SeniorId == seniorId && settings.SeniorEditingLocked, cancellationToken)
            ? SeniorProfileFailure.SeniorEditingLocked : null;
    }
}
