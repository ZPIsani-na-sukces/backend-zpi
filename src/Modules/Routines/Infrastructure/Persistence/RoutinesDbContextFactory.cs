using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.Routines.Infrastructure.Persistence;

public sealed class RoutinesDbContextFactory : IDesignTimeDbContextFactory<RoutinesDbContext>
{
    public RoutinesDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<RoutinesDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, RoutinesDbContext.Schema);
        return new RoutinesDbContext(options.Options);
    }
}
