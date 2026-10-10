using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Database;

namespace Zpi.Modules.SeniorProfile.Infrastructure.Persistence.Configurations;

internal sealed class SeniorContactPersonConfiguration : IEntityTypeConfiguration<SeniorContactPerson>
{
    public void Configure(EntityTypeBuilder<SeniorContactPerson> builder)
    {
        builder.ToTable("senior_contact_person", table =>
        {
            table.HasCheckConstraint("ck_senior_contact_person_phone", "phone_number ~ '^\\+[1-9][0-9]{1,14}$'");
            table.HasCheckConstraint("ck_senior_contact_person_version", "version >= 1");
            table.HasCheckConstraint("ck_seniorcontactperson_display_name_nonempty", "length(display_name) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(x => x.FirstName).HasColumnName("first_name");
        builder.Property(x => x.LastName).HasColumnName("last_name");
        builder.Property(x => x.RelationshipLabel).HasColumnName("relationship_label");
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedByAccountId).HasColumnName("deleted_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.SeniorId);
        builder.Property(x => x.DisplayName).HasMaxLength(100);
        builder.Property(x => x.FirstName).HasMaxLength(100);
        builder.Property(x => x.LastName).HasMaxLength(100);
        builder.Property(x => x.RelationshipLabel).HasMaxLength(100);
        builder.Property(x => x.PhoneNumber).HasMaxLength(16);
    }
}
