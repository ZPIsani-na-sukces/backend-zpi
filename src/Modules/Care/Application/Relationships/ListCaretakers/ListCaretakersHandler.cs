using Microsoft.EntityFrameworkCore;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Relationships.ListCaretakers;

public sealed class ListCaretakersHandler(CareDbContext context, IIdentityFacade identity, ICareFacade care)
{
    public async Task<CareResult<CursorPageDto<CareRelationshipDto>>> Handle(ListCaretakersQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return CareResult<CursorPageDto<CareRelationshipDto>>.Failed(CareFailure.ResourceNotFound);
        var rows = context.SeniorCareRelationships.AsNoTracking().Where(row =>
            row.SeniorId == query.SeniorId && row.Status == RelationshipStatus.Active);
        var caretakerIds = await rows.Select(row => row.CaretakerId).ToListAsync(cancellationToken);
        var page = await identity.GetActiveAccountCursorPageAsync(caretakerIds, null, query.Cursor, query.Limit, "caretakers:" + query.AccountId + ":" + query.SeniorId, cancellationToken);
        var accounts = page.Items.ToDictionary(account => account.UserId);
        var senior = await identity.GetActiveAccountsAsync([query.SeniorId], cancellationToken);
        if (!senior.TryGetValue(query.SeniorId, out var seniorAccount))
            return CareResult<CursorPageDto<CareRelationshipDto>>.Failed(CareFailure.ResourceNotFound);
        var pageIds = accounts.Keys.ToArray();
        accounts[query.SeniorId] = seniorAccount;
        var relationships = await rows.Where(row => pageIds.Contains(row.CaretakerId))
            .ToDictionaryAsync(row => row.CaretakerId, cancellationToken);
        var items = page.Items.Where(account => relationships.ContainsKey(account.UserId))
            .Select(account => CareDtoMapper.Relationship(relationships[account.UserId], accounts)).ToList();
        return CareResult<CursorPageDto<CareRelationshipDto>>.Success(new CursorPageDto<CareRelationshipDto>(items, page.NextCursor));
    }
}
