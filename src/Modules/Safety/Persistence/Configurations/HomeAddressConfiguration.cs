using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Safety.Domain;
using Zpi.Database;

namespace Zpi.Modules.Safety.Persistence.Configurations;

internal sealed class HomeAddressConfiguration : IEntityTypeConfiguration<HomeAddress>
{
    public void Configure(EntityTypeBuilder<HomeAddress> builder)
    {
        builder.ToTable("home_address", table =>
        {
            table.HasCheckConstraint("ck_home_address_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
            table.HasCheckConstraint("ck_home_address_country", "country_code ~ '^[A-Z]{2}$'");
            table.HasCheckConstraint("ck_home_address_version", "version >= 1");
            table.HasCheckConstraint("ck_homeaddress_city_nonempty", "length(city) >= 1");
            table.HasCheckConstraint("ck_homeaddress_building_number_nonempty", "length(building_number) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.CountryCode).HasColumnName("country_code").IsRequired();
        builder.Property(x => x.PostalCode).HasColumnName("postal_code").IsRequired();
        builder.Property(x => x.City).HasColumnName("city").IsRequired();
        builder.Property(x => x.Street).HasColumnName("street");
        builder.Property(x => x.BuildingNumber).HasColumnName("building_number").IsRequired();
        builder.Property(x => x.ApartmentNumber).HasColumnName("apartment_number");
        builder.Property(x => x.Latitude).HasColumnName("latitude");
        builder.Property(x => x.Longitude).HasColumnName("longitude");
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedByAccountId).HasColumnName("deleted_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.SeniorId).IsUnique();
        builder.Property(x => x.CountryCode).HasMaxLength(2);
        builder.Property(x => x.PostalCode).HasMaxLength(32);
        builder.Property(x => x.City).HasMaxLength(200);
        builder.Property(x => x.Street).HasMaxLength(200);
        builder.Property(x => x.BuildingNumber).HasMaxLength(32);
        builder.Property(x => x.ApartmentNumber).HasMaxLength(32);
    }
}
