using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Infrastructure.Persistence.Configurations;

internal sealed class GeoZoneConfiguration : IEntityTypeConfiguration<GeoZone>
{
    public void Configure(EntityTypeBuilder<GeoZone> builder)
    {
        builder.ToTable("geo_zone", table =>
        {
            table.HasCheckConstraint("ck_geo_zone_coordinates", "center_latitude BETWEEN -90 AND 90 AND center_longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_geo_zone_radius", "radius_meters BETWEEN 50 AND 10000");
            table.HasCheckConstraint("ck_geo_zone_version", "version >= 1");
            table.HasCheckConstraint("ck_geozone_name_nonempty", "length(name) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.Name).HasColumnName("name").IsRequired();
        builder.Property(x => x.CenterLatitude).HasColumnName("center_latitude");
        builder.Property(x => x.CenterLongitude).HasColumnName("center_longitude");
        builder.Property(x => x.RadiusMeters).HasColumnName("radius_meters");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.SmsOnExitEnabled).HasColumnName("sms_on_exit_enabled");
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedByAccountId).HasColumnName("deleted_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.SeniorId);
        builder.Property(x => x.Name).HasMaxLength(100);
    }
}
