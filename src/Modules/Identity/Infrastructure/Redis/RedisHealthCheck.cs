using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Zpi.Modules.Identity.Infrastructure.Redis;

public sealed class RedisHealthCheck(IConnectionMultiplexer connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await connection.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (RedisException)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.");
        }
    }
}
