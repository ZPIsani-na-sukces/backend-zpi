using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.MedicalRecords.Domain;

namespace Zpi.Modules.MedicalRecords.Persistence.Configurations;

internal sealed class MedicalAuditEntryConfiguration : IEntityTypeConfiguration<MedicalAuditEntry>
{
    public void Configure(EntityTypeBuilder<MedicalAuditEntry> builder)
    {
        builder.ToTable("medical_audit_entry");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(row => row.SeniorId).HasColumnName("senior_id");
        builder.Property(row => row.ActorAccountId).HasColumnName("actor_account_id");
        builder.Property(row => row.RecordId).HasColumnName("record_id");
        builder.Property(row => row.FileId).HasColumnName("file_id");
        builder.Property(row => row.Action).HasColumnName("action").HasMaxLength(40);
        builder.Property(row => row.OccurredAt).HasColumnName("occurred_at");
        builder.HasIndex(row => new { row.SeniorId, row.OccurredAt });
    }
}
