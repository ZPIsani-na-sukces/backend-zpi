using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Safety.Domain;
using Zpi.Database.Outbox;

namespace Zpi.Modules.Safety.Infrastructure.Persistence;

public sealed class SafetyDbContext(DbContextOptions<SafetyDbContext> options) : DbContext(options)
{
    public const string Schema = "safety";
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<HomeAddress> HomeAddresses => Set<HomeAddress>();
    public DbSet<GeoZone> GeoZones => Set<GeoZone>();
    public DbSet<LocationSample> LocationSamples => Set<LocationSample>();
    public DbSet<BatterySample> BatterySamples => Set<BatterySample>();
    public DbSet<SosIncident> SosIncidents => Set<SosIncident>();
    public DbSet<GeofenceIncident> GeofenceIncidents => Set<GeofenceIncident>();
    public DbSet<LowBatteryIncident> LowBatteryIncidents => Set<LowBatteryIncident>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SafetyDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }
}
