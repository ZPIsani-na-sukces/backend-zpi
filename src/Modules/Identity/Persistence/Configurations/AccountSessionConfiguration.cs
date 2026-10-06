using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Identity.Domain;
using Zpi.Database;

namespace Zpi.Modules.Identity.Persistence.Configurations;

internal sealed class AccountSessionConfiguration : IEntityTypeConfiguration<AccountSession>
{
    public void Configure(EntityTypeBuilder<AccountSession> builder)
    {
        builder.ToTable("account_session", table =>
        {
            table.HasCheckConstraint("ck_account_session_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_account_session_version", "version >= 1");
            table.HasCheckConstraint("ck_account_session_client_type", "client_type IN ('web','mobile')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.ClientType).HasColumnName("client_type").HasConversion(new CamelCaseEnumConverter<ClientType>()).HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.AccountId);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
