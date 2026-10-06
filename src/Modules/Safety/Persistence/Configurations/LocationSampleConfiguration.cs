using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Persistence.Configurations;

internal sealed class LocationSampleConfiguration : IEntityTypeConfiguration<LocationSample>
{
    public void Configure(EntityTypeBuilder<LocationSample> builder)
    {
        builder.ToTable("location_sample", table =>
        {
            table.HasCheckConstraint("ck_location_sample_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_location_sample_accuracy", "accuracy_meters >= 0");
            table.HasCheckConstraint("ck_location_sample_speed", "speed_mps >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.DeviceId).HasColumnName("device_id");
        builder.Property(x => x.Latitude).HasColumnName("latitude");
        builder.Property(x => x.Longitude).HasColumnName("longitude");
        builder.Property(x => x.AccuracyMeters).HasColumnName("accuracy_meters");
        builder.Property(x => x.SpeedMps).HasColumnName("speed_mps");
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(x => new { x.DeviceId, x.CapturedAt });
        builder.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
