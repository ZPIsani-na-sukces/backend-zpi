using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Infrastructure;

internal sealed class CareFacade(CareDbContext context, IIdentityFacade identity) : ICareFacade
{
    public async Task<IReadOnlyCollection<Guid>> GetActiveCaretakerIdsAsync(Guid seniorId, CancellationToken cancellationToken)
    {
        var ids = await context.SeniorCareRelationships
            .AsNoTracking()
            .Where(row => row.SeniorId == seniorId && row.Status == RelationshipStatus.Active)
            .Select(row => row.CaretakerId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync(ids.Append(seniorId).ToArray(), cancellationToken);
        if (!accounts.TryGetValue(seniorId, out var senior) || senior.Role != AccountRoles.Senior) return [];
        return ids.Where(id => accounts.TryGetValue(id, out var caretaker) && caretaker.Role == AccountRoles.Caretaker).ToArray();
    }

    public async Task<IReadOnlyCollection<Guid>> GetAccessibleSeniorIdsAsync(Guid accountId, IReadOnlyCollection<Guid> seniorIds,
        CancellationToken cancellationToken)
    {
        var accounts = await identity.GetActiveAccountsAsync(seniorIds.Append(accountId).Distinct().ToArray(), cancellationToken);
        if (!accounts.TryGetValue(accountId, out var account)) return [];
        if (account.Role == AccountRoles.Senior) return seniorIds.Contains(accountId) ? [accountId] : [];
        if (account.Role != AccountRoles.Caretaker) return [];
        var active = seniorIds.Where(id => accounts.TryGetValue(id, out var senior) && senior.Role == AccountRoles.Senior).ToArray();
        return await context.SeniorCareRelationships.AsNoTracking()
            .Where(row => row.CaretakerId == accountId && active.Contains(row.SeniorId) && row.Status == RelationshipStatus.Active)
            .Select(row => row.SeniorId).Distinct().ToArrayAsync(cancellationToken);
    }

    public async Task<bool> CanAccessSeniorAsync(Guid accountId, Guid seniorId, CancellationToken cancellationToken)
    {
        if (accountId != seniorId) return await HasActiveRelationshipAsync(accountId, seniorId, cancellationToken);
        var accounts = await identity.GetActiveAccountsAsync([seniorId], cancellationToken);
        return accounts.TryGetValue(seniorId, out var account) && account.Role == AccountRoles.Senior;
    }

    public async Task<bool> HasActiveRelationshipAsync(Guid caretakerId, Guid seniorId, CancellationToken cancellationToken)
    {
        var accounts = await identity.GetActiveAccountsAsync([caretakerId, seniorId], cancellationToken);
        return accounts.TryGetValue(caretakerId, out var caretaker) && caretaker.Role == AccountRoles.Caretaker &&
            accounts.TryGetValue(seniorId, out var senior) && senior.Role == AccountRoles.Senior &&
            await context.SeniorCareRelationships.AsNoTracking().AnyAsync(relationship =>
                relationship.CaretakerId == caretakerId && relationship.SeniorId == seniorId &&
                relationship.Status == RelationshipStatus.Active, cancellationToken);
    }
}
