using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.SeniorProfile.Persistence;

#nullable disable

namespace Zpi.Modules.SeniorProfile.Migrations
{
    [DbContext(typeof(SeniorProfileDbContext))]
    partial class SeniorProfileDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("profile")
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

                    b.ToTable("outbox_message", "profile", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.SeniorProfile.Domain.AccessibilityPreference", b =>
                {
                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<int>("FontScalePercent")
                        .HasColumnType("integer")
                        .HasColumnName("font_scale_percent");

                    b.Property<bool>("HighContrastEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("high_contrast_enabled");

                    b.Property<bool>("LargeButtonsEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("large_buttons_enabled");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<Guid>("UpdatedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("updated_by_account_id");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("SeniorId");

                    b.ToTable("accessibility_preference", "profile", t =>
                        {
                            t.HasCheckConstraint("ck_accessibility_preference_scale", "font_scale_percent BETWEEN 100 AND 400");

                            t.HasCheckConstraint("ck_accessibility_preference_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.SeniorProfile.Domain.SeniorContactPerson", b =>
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

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("display_name");

                    b.Property<string>("FirstName")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("first_name");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("LastName")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("last_name");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("phone_number");

                    b.Property<string>("RelationshipLabel")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("relationship_label");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

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

                    b.HasIndex("SeniorId");

                    b.ToTable("senior_contact_person", "profile", t =>
                        {
                            t.HasCheckConstraint("ck_senior_contact_person_phone", "phone_number ~ '^\\+[1-9][0-9]{1,14}$'");

                            t.HasCheckConstraint("ck_senior_contact_person_version", "version >= 1");

                            t.HasCheckConstraint("ck_seniorcontactperson_display_name_nonempty", "length(display_name) >= 1");
                        });
                });
#pragma warning restore 612, 618
        }
    }
}
