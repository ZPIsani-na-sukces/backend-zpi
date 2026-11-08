using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.UnverifiedAccountCleanup;

public sealed class UnverifiedAccountCleanupProcessor(IdentityDbContext context, TimeProvider clock,
    ILogger<UnverifiedAccountCleanupProcessor> logger)
{
    public static readonly TimeSpan MaximumAge = TimeSpan.FromDays(1);

    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - MaximumAge;
        var deleted = await context.Accounts.Where(account => account.Status == AccountStatus.PendingPhoneVerification &&
            account.PhoneVerifiedAt == null && account.CreatedAt <= cutoff).ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
            logger.LogInformation("Deleted {Count} accounts that did not verify their phone number within {MaximumAge}.",
                deleted, MaximumAge);
    }
}
