using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Zpi.Database;

namespace Zpi.Modules.Care.Infrastructure.Persistence;

public sealed class CareDbContextFactory : IDesignTimeDbContextFactory<CareDbContext>
{
    public CareDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
            ?? throw new InvalidOperationException("Set ConnectionStrings__Database before running dotnet ef.");
        var options = new DbContextOptionsBuilder<CareDbContext>();
        DatabaseConfiguration.Configure(options, connectionString, CareDbContext.Schema);
        return new CareDbContext(options.Options);
    }
}
