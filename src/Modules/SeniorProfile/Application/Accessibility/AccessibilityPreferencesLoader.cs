using Microsoft.EntityFrameworkCore;
using Npgsql;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Accessibility;

public sealed class AccessibilityPreferencesLoader(SeniorProfileDbContext context, TimeProvider clock)
{
    public async Task<AccessibilityPreference> GetOrCreateAsync(Guid seniorId, Guid accountId, CancellationToken cancellationToken)
    {
        var preference = await context.AccessibilityPreferences.SingleOrDefaultAsync(row => row.SeniorId == seniorId, cancellationToken);
        if (preference is not null) return preference;
        preference = new AccessibilityPreference
        {
            SeniorId = seniorId,
            UpdatedByAccountId = accountId,
            UpdatedAt = clock.GetUtcNow()
        };
        context.AccessibilityPreferences.Add(preference);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return preference;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            context.Entry(preference).State = EntityState.Detached;
            return await context.AccessibilityPreferences.SingleAsync(row => row.SeniorId == seniorId, cancellationToken);
        }
    }
}
