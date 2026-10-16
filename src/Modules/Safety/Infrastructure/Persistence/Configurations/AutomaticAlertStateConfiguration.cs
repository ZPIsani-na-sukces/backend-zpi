using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Infrastructure.Persistence.Configurations;

internal sealed class AutomaticAlertStateConfiguration : IEntityTypeConfiguration<AutomaticAlertState>
{
    public void Configure(EntityTypeBuilder<AutomaticAlertState> builder)
    {
        builder.ToTable("automatic_alert_state");
        builder.Property(row => row.SeniorId).HasColumnName("senior_id");
        builder.Property(row => row.DeviceId).HasColumnName("device_id");
        builder.Property(row => row.LocationEvaluatedAt).HasColumnName("location_evaluated_at");
        builder.Property(row => row.IsInside).HasColumnName("is_inside");
        builder.Property(row => row.CandidateInside).HasColumnName("candidate_inside");
        builder.Property(row => row.CandidateSince).HasColumnName("candidate_since");
        builder.Property(row => row.InsideZonesJson).HasColumnName("inside_zones_json");
        builder.Property(row => row.GeofenceIncidentId).HasColumnName("geofence_incident_id");
        builder.Property(row => row.GeofenceNotificationActive).HasColumnName("geofence_notification_active");
        builder.Property(row => row.BatteryEvaluatedAt).HasColumnName("battery_evaluated_at");
        builder.Property(row => row.BatteryArmed).HasColumnName("battery_armed");
        builder.Property(row => row.BatteryIncidentId).HasColumnName("battery_incident_id");
        builder.Property(row => row.BatteryNotificationActive).HasColumnName("battery_notification_active");
        builder.Property(row => row.Version).HasColumnName("version");
        builder.HasKey(row => row.SeniorId);
        builder.Property(row => row.SeniorId).ValueGeneratedNever();
        builder.Property(row => row.Version).IsConcurrencyToken();
        builder.Property(row => row.InsideZonesJson).HasColumnType("jsonb");
        builder.HasOne<Device>().WithMany().HasForeignKey(row => row.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
