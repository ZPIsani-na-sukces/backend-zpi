using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Identity.Domain;
using Zpi.Database;

namespace Zpi.Modules.Identity.Persistence.Configurations;

internal sealed class VerificationTicketConfiguration : IEntityTypeConfiguration<VerificationTicket>
{
    public void Configure(EntityTypeBuilder<VerificationTicket> builder)
    {
        builder.ToTable("verification_ticket", table =>
        {
            table.HasCheckConstraint("ck_verification_ticket_expiration", "expires_at > created_at");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.Property(x => x.TokenHash).HasMaxLength(128);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
