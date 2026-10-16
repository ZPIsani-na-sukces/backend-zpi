using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Infrastructure.Persistence.Configurations;

internal sealed class GeofenceIncidentConfiguration : IEntityTypeConfiguration<GeofenceIncident>
{
    public void Configure(EntityTypeBuilder<GeofenceIncident> builder)
    {
        builder.ToTable("geofence_incident", table =>
        {
            table.HasCheckConstraint("ck_geofence_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");
            table.HasCheckConstraint("ck_geofence_incident_accuracy", "accuracy_meters >= 0");
            table.HasCheckConstraint("ck_geofence_incident_speed", "speed_mps >= 0");
            table.HasCheckConstraint("ck_geofence_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");
            table.HasCheckConstraint("ck_geofence_incident_version", "version >= 1");
            table.HasCheckConstraint("ck_geofence_incident_status", "status IN ('open','acknowledged','resolved')");
        });
        builder.HasIndex(x => x.SeniorId).IsUnique().HasFilter("status <> 'resolved'");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.DeviceId).HasColumnName("device_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<IncidentStatus>()).HasMaxLength(40);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.Property(x => x.Latitude).HasColumnName("latitude");
        builder.Property(x => x.Longitude).HasColumnName("longitude");
        builder.Property(x => x.AccuracyMeters).HasColumnName("accuracy_meters");
        builder.Property(x => x.SpeedMps).HasColumnName("speed_mps");
        builder.Property(x => x.PositionCapturedAt).HasColumnName("position_captured_at");
        builder.Property(x => x.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(x => x.AcknowledgedByAccountId).HasColumnName("acknowledged_by_account_id");
        builder.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(x => x.ResolvedByAccountId).HasColumnName("resolved_by_account_id");
        builder.Property(x => x.ResolutionNote).HasColumnName("resolution_note");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.Property(x => x.GeoZoneId).HasColumnName("geo_zone_id");
        builder.Property(x => x.ZonesJson).HasColumnName("zones_json").HasColumnType("jsonb");
        builder.Property(x => x.ZoneName).HasColumnName("zone_name").IsRequired();
        builder.HasIndex(x => new { x.SeniorId, x.ReceivedAt });
        builder.Property(x => x.ResolutionNote).HasMaxLength(1000);
        builder.Property(x => x.ZoneName).HasMaxLength(100);
        builder.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeoZone>().WithMany().HasForeignKey(x => x.GeoZoneId).OnDelete(DeleteBehavior.Restrict);
    }
}
