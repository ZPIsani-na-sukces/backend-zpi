using Microsoft.EntityFrameworkCore;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Domain;
using Zpi.Modules.Care.Infrastructure.Persistence;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Care.Application.Invitations.List;

public sealed class ListInvitationsHandler(CareDbContext context, IIdentityFacade identity, TimeProvider time)
{
    public async Task<CareResult<PageDto<CareInvitationDto>>> Handle(ListInvitationsQuery query, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var rows = context.SeniorCareInvitations.AsNoTracking().Where(invitation => query.Direction == "sent"
            ? invitation.SenderAccountId == query.AccountId : invitation.RecipientAccountId == query.AccountId);
        if (query.Status is not null)
        {
            var status = Enum.Parse<InvitationStatus>(query.Status, true);
            rows = status switch
            {
                InvitationStatus.Expired => rows.Where(row => row.Status == InvitationStatus.Expired ||
                    row.Status == InvitationStatus.Pending && row.ExpiresAt <= now),
                InvitationStatus.Pending => rows.Where(row => row.Status == InvitationStatus.Pending && row.ExpiresAt > now),
                _ => rows.Where(row => row.Status == status)
            };
        }
        var parties = await rows.Select(row => new { row.SeniorId, row.CaretakerId }).ToListAsync(cancellationToken);
        var accountIds = parties.SelectMany(row => new[] { row.SeniorId, row.CaretakerId }).Distinct().ToArray();
        var accounts = await identity.GetActiveAccountsAsync(accountIds, cancellationToken);
        var activeIds = accounts.Keys.ToArray();
        rows = rows.Where(row => activeIds.Contains(row.SeniorId) && activeIds.Contains(row.CaretakerId));
        var total = await rows.CountAsync(cancellationToken);
        var invitations = await rows.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToListAsync(cancellationToken);
        var items = invitations.Select(row => CareDtoMapper.Invitation(row, accounts, now)).ToList();
        return CareResult<PageDto<CareInvitationDto>>.Success(new PageDto<CareInvitationDto>(items, query.Page, query.Limit, total));
    }
}
