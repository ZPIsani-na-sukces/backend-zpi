using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Database;
using Zpi.Modules.Care.Domain;

namespace Zpi.Modules.Care.Infrastructure.Persistence.Configurations;

internal sealed class SeniorCareInvitationConfiguration : IEntityTypeConfiguration<SeniorCareInvitation>
{
    public void Configure(EntityTypeBuilder<SeniorCareInvitation> builder)
    {
        builder.ToTable("senior_care_invitation", table =>
        {
            table.HasCheckConstraint("ck_senior_care_invitation_parties", "recipient_account_id IS NULL OR sender_account_id <> recipient_account_id");
            table.HasCheckConstraint("ck_senior_care_invitation_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_senior_care_invitation_version", "version >= 1");
            table.HasCheckConstraint("ck_senior_care_invitation_code", "code ~ '^[0-9]{6}$' AND length(code) = 6");
            table.HasCheckConstraint("ck_senior_care_invitation_status", "(status = 'pending' AND recipient_account_id IS NULL AND responded_at IS NULL AND relationship_id IS NULL) OR (status = 'accepted' AND recipient_account_id IS NOT NULL AND responded_at IS NOT NULL AND relationship_id IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SenderAccountId).HasColumnName("sender_account_id");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(6);
        builder.Property(x => x.RecipientAccountId).HasColumnName("recipient_account_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<InvitationStatus>()).HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RespondedAt).HasColumnName("responded_at");
        builder.Property(x => x.RelationshipId).HasColumnName("relationship_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.SenderAccountId).IsUnique().HasFilter("status = 'pending'");
        builder.HasIndex(x => x.RecipientAccountId);
        builder.HasIndex(x => x.RelationshipId).IsUnique();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasOne<SeniorCareRelationship>().WithMany().HasForeignKey(x => x.RelationshipId).OnDelete(DeleteBehavior.Restrict);
    }
}
