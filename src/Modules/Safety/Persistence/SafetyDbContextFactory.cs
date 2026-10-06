using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.Safety.Persistence;

public sealed class SafetyDbContextFactory : IDesignTimeDbContextFactory<SafetyDbContext>
{
    public SafetyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<SafetyDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, SafetyDbContext.Schema);
        return new SafetyDbContext(options.Options);
    }
}
