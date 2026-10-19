using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.MedicalRecords.Persistence;

#nullable disable

namespace Zpi.Modules.MedicalRecords.Migrations
{
    [DbContext(typeof(MedicalRecordsDbContext))]
    partial class MedicalRecordsDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("medical")
                .HasAnnotation("ProductVersion", "10.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Zpi.Database.Outbox.OutboxMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("Attempts")
                        .HasColumnType("integer")
                        .HasColumnName("attempts");

                    b.Property<int>("ContractVersion")
                        .HasColumnType("integer")
                        .HasColumnName("contract_version");

                    b.Property<string>("LastError")
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("last_error");

                    b.Property<DateTimeOffset?>("NextAttemptAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("next_attempt_at");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("payload");

                    b.Property<DateTimeOffset?>("ProcessedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("processed_at");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("character varying(300)")
                        .HasColumnName("type");

                    b.HasKey("Id");

                    b.HasIndex("NextAttemptAt", "OccurredAt")
                        .HasFilter("processed_at IS NULL");

                    b.ToTable("outbox_message", "medical", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.MedicalRecords.Domain.MedicalAuditEntry", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("action");

                    b.Property<Guid?>("ActorAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("actor_account_id");

                    b.Property<Guid?>("FileId")
                        .HasColumnType("uuid")
                        .HasColumnName("file_id");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<Guid?>("RecordId")
                        .HasColumnType("uuid")
                        .HasColumnName("record_id");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.HasKey("Id");

                    b.HasIndex("SeniorId", "OccurredAt");

                    b.ToTable("medical_audit_entry", "medical");
                });

            modelBuilder.Entity("Zpi.Modules.MedicalRecords.Domain.MedicalFile", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("ChecksumSha256")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)")
                        .HasColumnName("checksum_sha256");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid>("CreatedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("created_by_account_id");

                    b.Property<DateTimeOffset?>("DeletedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_at");

                    b.Property<Guid>("MedicalRecordId")
                        .HasColumnType("uuid")
                        .HasColumnName("medical_record_id");

                    b.Property<string>("MimeType")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("mime_type");

                    b.Property<string>("ObjectKey")
                        .IsRequired()
                        .HasMaxLength(512)
                        .HasColumnType("character varying(512)")
                        .HasColumnName("object_key");

                    b.Property<string>("OriginalName")
                        .IsRequired()
                        .HasMaxLength(255)
                        .HasColumnType("character varying(255)")
                        .HasColumnName("original_name");

                    b.Property<DateTimeOffset?>("PurgedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("purged_at");

                    b.Property<long>("SizeBytes")
                        .HasColumnType("bigint")
                        .HasColumnName("size_bytes");

                    b.HasKey("Id");

                    b.HasIndex("MedicalRecordId");

                    b.HasIndex("ObjectKey")
                        .IsUnique();

                    b.ToTable("medical_file", "medical", t =>
                        {
                            t.HasCheckConstraint("ck_medical_file_checksum", "checksum_sha256 ~ '^[0-9a-f]{64}$'");

                            t.HasCheckConstraint("ck_medical_file_mime", "mime_type IN ('application/pdf','image/jpeg','image/png')");

                            t.HasCheckConstraint("ck_medical_file_size", "size_bytes BETWEEN 0 AND 20971520");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.MedicalRecords.Domain.MedicalRecord", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<Guid>("CreatedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("created_by_account_id");

                    b.Property<DateTimeOffset?>("DeletedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("deleted_at");

                    b.Property<Guid?>("DeletedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("deleted_by_account_id");

                    b.Property<string>("Description")
                        .HasMaxLength(4000)
                        .HasColumnType("character varying(4000)")
                        .HasColumnName("description");

                    b.Property<DateOnly>("DocumentDate")
                        .HasColumnType("date")
                        .HasColumnName("document_date");

                    b.Property<string>("RecordType")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("record_type");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("title");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<Guid?>("UpdatedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("updated_by_account_id");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("SeniorId", "DocumentDate");

                    b.ToTable("medical_record", "medical", t =>
                        {
                            t.HasCheckConstraint("ck_medical_record_record_type", "record_type IN ('testResult','prescription','dischargeSummary','consultation','other')");

                            t.HasCheckConstraint("ck_medical_record_version", "version >= 1");

                            t.HasCheckConstraint("ck_medicalrecord_title_nonempty", "length(title) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.MedicalRecords.Domain.MedicalFile", b =>
                {
                    b.HasOne("Zpi.Modules.MedicalRecords.Domain.MedicalRecord", null)
                        .WithMany()
                        .HasForeignKey("MedicalRecordId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
