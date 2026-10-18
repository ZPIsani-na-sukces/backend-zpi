using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Zpi.Modules.MedicalRecords.Domain;
using Zpi.Database;

namespace Zpi.Modules.MedicalRecords.Persistence.Configurations;

internal sealed class MedicalFileConfiguration : IEntityTypeConfiguration<MedicalFile>
{
    public void Configure(EntityTypeBuilder<MedicalFile> builder)
    {
        builder.ToTable("medical_file", table =>
        {
            table.HasCheckConstraint("ck_medical_file_size", "size_bytes BETWEEN 0 AND 20971520");
            table.HasCheckConstraint("ck_medical_file_mime", "mime_type IN ('application/pdf','image/jpeg','image/png')");
            table.HasCheckConstraint("ck_medical_file_checksum", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MedicalRecordId).HasColumnName("medical_record_id");
        builder.Property(x => x.ObjectKey).HasColumnName("object_key").IsRequired();
        builder.Property(x => x.OriginalName).HasColumnName("original_name").IsRequired();
        builder.Property(x => x.MimeType).HasColumnName("mime_type").IsRequired();
        builder.Property(x => x.SizeBytes).HasColumnName("size_bytes");
        builder.Property(x => x.ChecksumSha256).HasColumnName("checksum_sha256").IsRequired();
        builder.Property(x => x.CreatedByAccountId).HasColumnName("created_by_account_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.PurgedAt).HasColumnName("purged_at");
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.HasIndex(x => x.ObjectKey).IsUnique();
        builder.Property(x => x.ObjectKey).HasMaxLength(512);
        builder.Property(x => x.OriginalName).HasMaxLength(255);
        builder.Property(x => x.MimeType).HasMaxLength(100);
        builder.Property(x => x.ChecksumSha256).HasMaxLength(64);
        builder.HasOne<MedicalRecord>().WithMany().HasForeignKey(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Restrict);
    }
}
