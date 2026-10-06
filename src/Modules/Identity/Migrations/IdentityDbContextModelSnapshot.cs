using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.Identity.Persistence;

#nullable disable

namespace Zpi.Modules.Identity.Migrations
{
    [DbContext(typeof(IdentityDbContext))]
    partial class IdentityDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("identity")
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

                    b.ToTable("outbox_message", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.Account", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Email")
                        .HasMaxLength(254)
                        .HasColumnType("character varying(254)")
                        .HasColumnName("email");

                    b.Property<DateTimeOffset?>("EmailVerifiedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("email_verified_at");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("first_name");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("last_name");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(254)
                        .HasColumnType("character varying(254)")
                        .HasColumnName("normalized_email");

                    b.Property<string>("NormalizedPhoneNumber")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("normalized_phone_number");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("character varying(1024)")
                        .HasColumnName("password_hash");

                    b.Property<string>("PhoneNumber")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("character varying(16)")
                        .HasColumnName("phone_number");

                    b.Property<DateTimeOffset?>("PhoneVerifiedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("phone_verified_at");

                    b.Property<Guid>("RoleId")
                        .HasColumnType("uuid")
                        .HasColumnName("role_id");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .IsUnique();

                    b.HasIndex("NormalizedPhoneNumber")
                        .IsUnique();

                    b.HasIndex("RoleId");

                    b.ToTable("account", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_account_email_verification", "email_verified_at IS NULL OR email IS NOT NULL");

                            t.HasCheckConstraint("ck_account_first_name_nonempty", "length(first_name) >= 1");

                            t.HasCheckConstraint("ck_account_last_name_nonempty", "length(last_name) >= 1");

                            t.HasCheckConstraint("ck_account_password_hash_nonempty", "length(password_hash) >= 1");

                            t.HasCheckConstraint("ck_account_phone", "phone_number ~ '^\\+[1-9][0-9]{1,14}$'");

                            t.HasCheckConstraint("ck_account_status", "status IN ('pendingPhoneVerification','active','blocked')");

                            t.HasCheckConstraint("ck_account_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.AccountSession", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("account_id");

                    b.Property<string>("ClientType")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("client_type");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<DateTimeOffset?>("RevokedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("revoked_at");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("account_session", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_account_session_client_type", "client_type IN ('web','mobile')");

                            t.HasCheckConstraint("ck_account_session_expiration", "expires_at > created_at");

                            t.HasCheckConstraint("ck_account_session_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.RefreshToken", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<DateTimeOffset?>("ConsumedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("consumed_at");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<DateTimeOffset?>("RevokedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("revoked_at");

                    b.Property<Guid>("SessionId")
                        .HasColumnType("uuid")
                        .HasColumnName("session_id");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("token_hash");

                    b.HasKey("Id");

                    b.HasIndex("SessionId");

                    b.HasIndex("TokenHash")
                        .IsUnique();

                    b.ToTable("refresh_token", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_refresh_token_expiration", "expires_at > created_at");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.Role", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Code")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("code");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("name");

                    b.HasKey("Id");

                    b.HasIndex("Code")
                        .IsUnique();

                    b.ToTable("role", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_role_code", "code IN ('senior','caretaker')");
                        });

                    b.HasData(
                        new
                        {
                            Id = new Guid("019a0000-0000-7000-8000-000000000001"),
                            Code = "senior",
                            Name = "Senior"
                        },
                        new
                        {
                            Id = new Guid("019a0000-0000-7000-8000-000000000002"),
                            Code = "caretaker",
                            Name = "Caretaker"
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.VerificationChallenge", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid?>("AccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("account_id");

                    b.Property<int>("Attempts")
                        .HasColumnType("integer")
                        .HasColumnName("attempts");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("channel");

                    b.Property<DateTimeOffset?>("ConsumedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("consumed_at");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Destination")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("character varying(254)")
                        .HasColumnName("destination");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<DateTimeOffset?>("InvalidatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("invalidated_at");

                    b.Property<string>("Purpose")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("purpose");

                    b.Property<DateTimeOffset>("ResendAfter")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("resend_after");

                    b.Property<string>("SecretHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("secret_hash");

                    b.Property<Guid?>("TicketId")
                        .HasColumnType("uuid")
                        .HasColumnName("ticket_id");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("TicketId");

                    b.ToTable("verification_challenge", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_verification_challenge_attempts", "attempts BETWEEN 0 AND 5");

                            t.HasCheckConstraint("ck_verification_challenge_channel", "channel IN ('sms','email')");

                            t.HasCheckConstraint("ck_verification_challenge_expiration", "expires_at > created_at");

                            t.HasCheckConstraint("ck_verification_challenge_purpose", "purpose IN ('phoneVerification','emailVerification','passwordReset')");

                            t.HasCheckConstraint("ck_verification_challenge_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.VerificationTicket", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<Guid>("AccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("account_id");

                    b.Property<DateTimeOffset?>("ConsumedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("consumed_at");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("expires_at");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("token_hash");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("TokenHash")
                        .IsUnique();

                    b.ToTable("verification_ticket", "identity", t =>
                        {
                            t.HasCheckConstraint("ck_verification_ticket_expiration", "expires_at > created_at");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.Account", b =>
                {
                    b.HasOne("Zpi.Modules.Identity.Domain.Role", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.AccountSession", b =>
                {
                    b.HasOne("Zpi.Modules.Identity.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.RefreshToken", b =>
                {
                    b.HasOne("Zpi.Modules.Identity.Domain.AccountSession", null)
                        .WithMany()
                        .HasForeignKey("SessionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.VerificationChallenge", b =>
                {
                    b.HasOne("Zpi.Modules.Identity.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.HasOne("Zpi.Modules.Identity.Domain.VerificationTicket", null)
                        .WithMany()
                        .HasForeignKey("TicketId")
                        .OnDelete(DeleteBehavior.Restrict);
                });

            modelBuilder.Entity("Zpi.Modules.Identity.Domain.VerificationTicket", b =>
                {
                    b.HasOne("Zpi.Modules.Identity.Domain.Account", null)
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
