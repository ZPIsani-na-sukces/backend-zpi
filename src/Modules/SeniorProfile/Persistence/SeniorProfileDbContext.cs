using Microsoft.EntityFrameworkCore;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Database.Outbox;

namespace Zpi.Modules.SeniorProfile.Persistence;

public sealed class SeniorProfileDbContext(DbContextOptions<SeniorProfileDbContext> options) : DbContext(options)
{
    public const string Schema = "profile";
    public DbSet<SeniorContactPerson> SeniorContactPersons => Set<SeniorContactPerson>();
    public DbSet<AccessibilityPreference> AccessibilityPreferences => Set<AccessibilityPreference>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SeniorProfileDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
