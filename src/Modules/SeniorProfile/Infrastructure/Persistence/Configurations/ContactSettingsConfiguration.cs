using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Infrastructure.Persistence.Configurations;

internal sealed class ContactSettingsConfiguration : IEntityTypeConfiguration<ContactSettings>
{
    public void Configure(EntityTypeBuilder<ContactSettings> builder)
    {
        builder.ToTable("contact_settings", table =>
            table.HasCheckConstraint("ck_contact_settings_version", "version >= 1"));
        builder.HasKey(settings => settings.SeniorId);
        builder.Property(settings => settings.SeniorId).HasColumnName("senior_id").ValueGeneratedNever();
        builder.Property(settings => settings.SeniorEditingLocked).HasColumnName("senior_editing_locked");
        builder.Property(settings => settings.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
