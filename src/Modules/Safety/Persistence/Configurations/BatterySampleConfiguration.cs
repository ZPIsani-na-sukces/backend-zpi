using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Persistence.Configurations;

internal sealed class BatterySampleConfiguration : IEntityTypeConfiguration<BatterySample>
{
    public void Configure(EntityTypeBuilder<BatterySample> builder)
    {
        builder.ToTable("battery_sample", table =>
        {
            table.HasCheckConstraint("ck_battery_sample_battery", "battery_level_percent BETWEEN 0 AND 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.DeviceId).HasColumnName("device_id");
        builder.Property(x => x.BatteryLevelPercent).HasColumnName("battery_level_percent");
        builder.Property(x => x.IsCharging).HasColumnName("is_charging");
        builder.Property(x => x.CapturedAt).HasColumnName("captured_at");
        builder.Property(x => x.ReceivedAt).HasColumnName("received_at");
        builder.HasIndex(x => new { x.DeviceId, x.CapturedAt });
        builder.HasOne<Device>().WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Restrict);
    }
}
