using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Infrastructure.Persistence;

namespace Zpi.Modules.Care.Application.Invitations.Cleanup;

public sealed class InvitationCleanupProcessor(CareDbContext context, TimeProvider clock)
{
    public Task<int> ProcessAsync(CancellationToken cancellationToken) => context.SeniorCareInvitations
        .Where(row => row.ExpiresAt <= clock.GetUtcNow())
        .ExecuteDeleteAsync(cancellationToken);
}
