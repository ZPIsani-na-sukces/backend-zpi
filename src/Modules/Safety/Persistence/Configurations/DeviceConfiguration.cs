using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("device", table =>
        {
            table.HasCheckConstraint("ck_device_retired", "retired_at IS NULL OR NOT monitoring_enabled");
            table.HasCheckConstraint("ck_device_version", "version >= 1");
            table.HasCheckConstraint("ck_device_platform", "platform IN ('android','ios')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AssignedAccountId).HasColumnName("assigned_account_id");
        builder.Property(x => x.InstallationId).HasColumnName("installation_id");
        builder.Property(x => x.Platform).HasColumnName("platform").HasConversion(new CamelCaseEnumConverter<DevicePlatform>()).HasMaxLength(40);
        builder.Property(x => x.AppVersion).HasColumnName("app_version").IsRequired();
        builder.Property(x => x.MonitoringEnabled).HasColumnName("monitoring_enabled");
        builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
        builder.Property(x => x.LastOnlineAt).HasColumnName("last_online_at");
        builder.Property(x => x.RetiredAt).HasColumnName("retired_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.InstallationId).IsUnique();
        builder.HasIndex(x => x.AssignedAccountId).IsUnique().HasFilter("monitoring_enabled = true AND retired_at IS NULL");
        builder.Property(x => x.AppVersion).HasMaxLength(100);
    }
}
