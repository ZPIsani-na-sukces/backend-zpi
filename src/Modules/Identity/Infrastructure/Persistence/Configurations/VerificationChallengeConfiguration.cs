using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Database;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Infrastructure.Persistence.Configurations;

internal sealed class VerificationChallengeConfiguration : IEntityTypeConfiguration<VerificationChallenge>
{
    public void Configure(EntityTypeBuilder<VerificationChallenge> builder)
    {
        builder.ToTable("verification_challenge", table =>
        {
            table.HasCheckConstraint("ck_verification_challenge_attempts", "attempts BETWEEN 0 AND 5");
            table.HasCheckConstraint("ck_verification_challenge_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_verification_challenge_version", "version >= 1");
            table.HasCheckConstraint("ck_verification_challenge_purpose", "purpose IN ('phoneVerification','emailVerification','passwordReset')");
            table.HasCheckConstraint("ck_verification_challenge_channel", "channel IN ('sms','email')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.TicketId).HasColumnName("ticket_id");
        builder.Property(x => x.Purpose).HasColumnName("purpose").HasConversion(new CamelCaseEnumConverter<ChallengePurpose>()).HasMaxLength(40);
        builder.Property(x => x.Channel).HasColumnName("channel").HasConversion(new CamelCaseEnumConverter<ChallengeChannel>()).HasMaxLength(40);
        builder.Property(x => x.Destination).HasColumnName("destination").IsRequired();
        builder.Property(x => x.SecretHash).HasColumnName("secret_hash").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.ResendAfter).HasColumnName("resend_after");
        builder.Property(x => x.Attempts).HasColumnName("attempts");
        builder.Property(x => x.ConsumedAt).HasColumnName("consumed_at");
        builder.Property(x => x.InvalidatedAt).HasColumnName("invalidated_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.AccountId);
        builder.Property(x => x.Destination).HasMaxLength(254);
        builder.Property(x => x.SecretHash).HasMaxLength(128);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VerificationTicket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
    }
}
