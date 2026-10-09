using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Care.Domain;
using Zpi.Database;

namespace Zpi.Modules.Care.Infrastructure.Persistence.Configurations;

internal sealed class SeniorCareInvitationConfiguration : IEntityTypeConfiguration<SeniorCareInvitation>
{
    public void Configure(EntityTypeBuilder<SeniorCareInvitation> builder)
    {
        builder.ToTable("senior_care_invitation", table =>
        {
            table.HasCheckConstraint("ck_senior_care_invitation_parties", "senior_id <> caretaker_id AND sender_account_id = caretaker_id AND recipient_account_id = senior_id");
            table.HasCheckConstraint("ck_senior_care_invitation_expiration", "expires_at > created_at");
            table.HasCheckConstraint("ck_senior_care_invitation_version", "version >= 1");
            table.HasCheckConstraint("ck_senior_care_invitation_status", "status IN ('pending','accepted','rejected','cancelled','expired')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.CaretakerId).HasColumnName("caretaker_id");
        builder.Property(x => x.SenderAccountId).HasColumnName("sender_account_id");
        builder.Property(x => x.RecipientAccountId).HasColumnName("recipient_account_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<InvitationStatus>()).HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        builder.Property(x => x.RespondedAt).HasColumnName("responded_at");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.SeniorId, x.CaretakerId }).IsUnique().HasFilter("status = 'pending'");
        builder.HasIndex(x => x.RecipientAccountId);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
