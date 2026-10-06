using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.Identity.Persistence;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, IdentityDbContext.Schema);
        return new IdentityDbContext(options.Options);
    }
}
