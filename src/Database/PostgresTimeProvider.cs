namespace Zpi.Database;

// PostgreSQL stores timestamps with microsecond precision; Linux clocks have 100 ns precision.
public sealed class PostgresTimeProvider : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        var now = System.GetUtcNow();
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }
}
