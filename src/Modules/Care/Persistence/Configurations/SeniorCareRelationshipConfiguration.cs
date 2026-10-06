using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.Care.Domain;
using Zpi.Database;

namespace Zpi.Modules.Care.Persistence.Configurations;

internal sealed class SeniorCareRelationshipConfiguration : IEntityTypeConfiguration<SeniorCareRelationship>
{
    public void Configure(EntityTypeBuilder<SeniorCareRelationship> builder)
    {
        builder.ToTable("senior_care_relationship", table =>
        {
            table.HasCheckConstraint("ck_senior_care_relationship_parties", "senior_id <> caretaker_id");
            table.HasCheckConstraint("ck_senior_care_relationship_removed", "(status = 'active' AND removed_at IS NULL) OR (status = 'removed' AND removed_at IS NOT NULL)");
            table.HasCheckConstraint("ck_senior_care_relationship_version", "version >= 1");
            table.HasCheckConstraint("ck_senior_care_relationship_status", "status IN ('active','removed')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.InvitationId).HasColumnName("invitation_id");
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.CaretakerId).HasColumnName("caretaker_id");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion(new CamelCaseEnumConverter<RelationshipStatus>()).HasMaxLength(40);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.RemovedAt).HasColumnName("removed_at");
        builder.Property(x => x.RemovedByAccountId).HasColumnName("removed_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => x.InvitationId).IsUnique();
        builder.HasIndex(x => new { x.SeniorId, x.CaretakerId }).IsUnique().HasFilter("status = 'active'");
        builder.HasIndex(x => x.CaretakerId);
        builder.HasOne<SeniorCareInvitation>().WithMany().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Restrict);
    }
}
