using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.Care.Infrastructure.Persistence;

#nullable disable

namespace Zpi.Modules.Care.Migrations
{
    [DbContext(typeof(CareDbContext))]
    [Migration("20261027143813_InvitationCodes")]
    partial class InvitationCodes
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("care")
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

                    b.ToTable("outbox_message", "care", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Care.Domain.SeniorCareInvitation", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(6)
                        .HasColumnType("character varying(6)")
                        .HasColumnName("code");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<Guid?>("RecipientAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("recipient_account_id");

                    b.Property<Guid?>("RelationshipId")
                        .HasColumnType("uuid")
                        .HasColumnName("relationship_id");

                    b.Property<DateTimeOffset?>("RespondedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("responded_at");

                    b.Property<Guid>("SenderAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("sender_account_id");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.HasIndex("ExpiresAt");

                    b.HasIndex("RecipientAccountId");

                    b.HasIndex("RelationshipId")
                        .IsUnique();

                    b.HasIndex("SenderAccountId")
                        .IsUnique()
                        .HasFilter("status = 'pending'");

                    b.ToTable("senior_care_invitation", "care", t =>
                        {
                            t.HasCheckConstraint("ck_senior_care_invitation_code", "code ~ '^[0-9]{6}$' AND length(code) = 6");

                            t.HasCheckConstraint("ck_senior_care_invitation_expiration", "expires_at > created_at");

                            t.HasCheckConstraint("ck_senior_care_invitation_parties", "recipient_account_id IS NULL OR sender_account_id <> recipient_account_id");

                            t.HasCheckConstraint("ck_senior_care_invitation_status", "(status = 'pending' AND recipient_account_id IS NULL AND responded_at IS NULL AND relationship_id IS NULL) OR (status = 'accepted' AND recipient_account_id IS NOT NULL AND responded_at IS NOT NULL AND relationship_id IS NOT NULL)");

                            t.HasCheckConstraint("ck_senior_care_invitation_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Care.Domain.SeniorCareRelationship", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("CaretakerId")
                        .HasColumnType("uuid")
                        .HasColumnName("caretaker_id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTimeOffset?>("RemovedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("removed_at");

                    b.Property<Guid?>("RemovedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("removed_by_account_id");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("CaretakerId");

                    b.HasIndex("SeniorId", "CaretakerId")
                        .IsUnique()
                        .HasFilter("status = 'active'");

                    b.ToTable("senior_care_relationship", "care", t =>
                        {
                            t.HasCheckConstraint("ck_senior_care_relationship_parties", "senior_id <> caretaker_id");

                            t.HasCheckConstraint("ck_senior_care_relationship_removed", "(status = 'active' AND removed_at IS NULL) OR (status = 'removed' AND removed_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_senior_care_relationship_status", "status IN ('active','removed')");

                            t.HasCheckConstraint("ck_senior_care_relationship_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Care.Domain.SeniorCareInvitation", b =>
                {
                    b.HasOne("Zpi.Modules.Care.Domain.SeniorCareRelationship", null)
                        .WithMany()
                        .HasForeignKey("RelationshipId")
                        .OnDelete(DeleteBehavior.Restrict);
                });
#pragma warning restore 612, 618
        }
    }
}
