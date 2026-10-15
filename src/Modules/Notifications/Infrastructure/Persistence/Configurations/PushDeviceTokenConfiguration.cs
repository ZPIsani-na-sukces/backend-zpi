using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Notifications.Domain;
using Zpi.Database;

namespace Zpi.Modules.Notifications.Infrastructure.Persistence.Configurations;

internal sealed class PushDeviceTokenConfiguration : IEntityTypeConfiguration<PushDeviceToken>
{
    public void Configure(EntityTypeBuilder<PushDeviceToken> builder)
    {
        builder.ToTable("push_device_token", table =>
        {
            table.HasCheckConstraint("ck_push_device_token_platform", "platform IN ('android','ios')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.SessionId).HasColumnName("session_id");
        builder.Property(x => x.InstallationId).HasColumnName("installation_id");
        builder.Property(x => x.Platform).HasColumnName("platform").HasConversion(new CamelCaseEnumConverter<PushPlatform>()).HasMaxLength(40);
        builder.Property(x => x.Token).HasColumnName("token").IsRequired();
        builder.Property(x => x.RegisteredAt).HasColumnName("registered_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.SessionId, x.InstallationId }).IsUnique().HasFilter("revoked_at IS NULL");
        builder.Property(x => x.Token).HasMaxLength(4096);
        builder.HasIndex(x => x.AccountId);
    }
}
