using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Zpi.Database;

public sealed class DatabaseHealthCheck<TContext>(TContext context) : IHealthCheck where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext,
        CancellationToken cancellationToken = default)
    {
        if (!await context.Database.CanConnectAsync(cancellationToken))
            return HealthCheckResult.Unhealthy("Database is unavailable.");

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        return pendingMigrations.Any()
            ? HealthCheckResult.Unhealthy("Module database migrations have not been applied.")
            : HealthCheckResult.Healthy();
    }
}
