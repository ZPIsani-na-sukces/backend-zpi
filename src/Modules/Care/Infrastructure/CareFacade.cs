using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Infrastructure;

internal sealed class CareFacade(CareDbContext context, IIdentityFacade identity) : ICareFacade
{
    public async Task<bool> CanAccessSeniorAsync(Guid accountId, Guid seniorId, CancellationToken cancellationToken)
    {
        if (accountId != seniorId) return await HasActiveRelationshipAsync(accountId, seniorId, cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync([seniorId], cancellationToken);
        return accounts.TryGetValue(seniorId, out var account) && account.Role == "senior";
    }

    public async Task<bool> HasActiveRelationshipAsync(Guid caretakerId, Guid seniorId, CancellationToken cancellationToken)
    {
        var accounts = await identity.GetActiveAccountsAsync([caretakerId, seniorId], cancellationToken);
        return accounts.TryGetValue(caretakerId, out var caretaker) && caretaker.Role == "caretaker" &&
            accounts.TryGetValue(seniorId, out var senior) && senior.Role == "senior" &&
            await context.SeniorCareRelationships.AsNoTracking().AnyAsync(relationship =>
                relationship.CaretakerId == caretakerId && relationship.SeniorId == seniorId &&
                relationship.Status == RelationshipStatus.Active, cancellationToken);
    }
}
