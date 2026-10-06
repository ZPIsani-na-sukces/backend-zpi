using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Database;

namespace Zpi.Modules.MedicalRecords.Persistence.Configurations;

internal sealed class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.ToTable("medical_record", table =>
        {
            table.HasCheckConstraint("ck_medical_record_version", "version >= 1");
            table.HasCheckConstraint("ck_medical_record_record_type", "record_type IN ('testResult','prescription','dischargeSummary','consultation','other')");
            table.HasCheckConstraint("ck_medicalrecord_title_nonempty", "length(title) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.SeniorId).HasColumnName("senior_id");
        builder.Property(x => x.Title).HasColumnName("title").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.RecordType).HasColumnName("record_type").HasConversion(new CamelCaseEnumConverter<MedicalRecordType>()).HasMaxLength(40);
        builder.Property(x => x.DocumentDate).HasColumnName("document_date");
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.UpdatedByAccountId).HasColumnName("updated_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedByAccountId).HasColumnName("deleted_by_account_id");
        builder.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        builder.HasIndex(x => new { x.SeniorId, x.DocumentDate });
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(4000);
    }
}
