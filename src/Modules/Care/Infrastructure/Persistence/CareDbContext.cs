using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Domain;
using Zpi.Database.Outbox;

namespace Zpi.Modules.Care.Infrastructure.Persistence;

public sealed class CareDbContext(DbContextOptions<CareDbContext> options) : DbContext(options)
{
    public const string Schema = "care";
    public DbSet<SeniorCareInvitation> SeniorCareInvitations => Set<SeniorCareInvitation>();
    public DbSet<SeniorCareRelationship> SeniorCareRelationships => Set<SeniorCareRelationship>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
