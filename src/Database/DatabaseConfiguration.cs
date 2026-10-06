using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Zpi.Database;

public static class DatabaseConfiguration
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString, string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        options.UseNpgsql(connectionString, postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", schema));
        options.AddInterceptors(new VersionInterceptor());
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        return !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new InvalidOperationException("Configure ConnectionStrings:Database before starting the backend.");
    }
}
