using Microsoft.EntityFrameworkCore;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed class ListSeniorsHandler(CareDbContext context, IIdentityFacade identity)
{
    public async Task<CareResult<CursorPageDto<SeniorSummaryDto>>> Handle(ListSeniorsQuery query, CancellationToken cancellationToken)
    {
        var rows = context.SeniorCareRelationships.AsNoTracking().Where(row =>
            row.CaretakerId == query.AccountId && row.Status == RelationshipStatus.Active);
        var seniorIds = await rows.Select(row => row.SeniorId).ToListAsync(cancellationToken);
        var accounts = await identity.GetActiveAccountCursorPageAsync(seniorIds, query.Search, query.Cursor, query.Limit, "seniors:" + query.AccountId, cancellationToken);
        var pageIds = accounts.Items.Select(account => account.UserId).ToArray();
        var relationships = await rows.Where(row => pageIds.Contains(row.SeniorId))
            .ToDictionaryAsync(row => row.SeniorId, row => row.Id, cancellationToken);
        var items = accounts.Items.Where(account => relationships.ContainsKey(account.UserId))
            .Select(account => new SeniorSummaryDto(account, relationships[account.UserId])).ToList();
        return CareResult<CursorPageDto<SeniorSummaryDto>>.Success(new CursorPageDto<SeniorSummaryDto>(items, accounts.NextCursor));
    }
}
