using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Database;

namespace Zpi.Modules.SeniorProfile.Persistence.Configurations;

internal sealed class AccessibilityPreferenceConfiguration : IEntityTypeConfiguration<AccessibilityPreference>
{
    public void Configure(EntityTypeBuilder<AccessibilityPreference> builder)
    {
        builder.ToTable("accessibility_preference", table =>
        {
            table.HasCheckConstraint("ck_accessibility_preference_scale", "font_scale_percent BETWEEN 100 AND 400");
            table.HasCheckConstraint("ck_accessibility_preference_version", "version >= 1");
        });
        builder.HasKey(x => x.SeniorId);
        builder.Property(x => x.SeniorId).HasColumnName("senior_id").ValueGeneratedNever();
        builder.Property(x => x.FontScalePercent).HasColumnName("font_scale_percent");
        builder.Property(x => x.HighContrastEnabled).HasColumnName("high_contrast_enabled");
        builder.Property(x => x.LargeButtonsEnabled).HasColumnName("large_buttons_enabled");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
