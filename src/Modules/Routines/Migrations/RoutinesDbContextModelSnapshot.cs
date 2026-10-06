using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.Routines.Persistence;

#nullable disable

namespace Zpi.Modules.Routines.Migrations
{
    [DbContext(typeof(RoutinesDbContext))]
    partial class RoutinesDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("routines")
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

                    b.ToTable("outbox_message", "routines", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.RoutineTask", b =>
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

                    b.Property<DateOnly?>("EndsOn")
                        .HasColumnType("date")
                        .HasColumnName("ends_on");

                    b.Property<string>("Instructions")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("instructions");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<int>("ScheduleRevision")
                        .HasColumnType("integer")
                        .HasColumnName("schedule_revision");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<DateOnly>("StartsOn")
                        .HasColumnType("date")
                        .HasColumnName("starts_on");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)")
                        .HasColumnName("title");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("type");

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

                    b.ToTable("routine_task", "routines", t =>
                        {
                            t.HasCheckConstraint("ck_routine_task_dates", "ends_on IS NULL OR ends_on >= starts_on");

                            t.HasCheckConstraint("ck_routine_task_revision", "schedule_revision >= 1");

                            t.HasCheckConstraint("ck_routine_task_type", "type IN ('medication','meal','hydration')");

                            t.HasCheckConstraint("ck_routine_task_version", "version >= 1");

                            t.HasCheckConstraint("ck_routinetask_instructions_nonempty", "length(instructions) >= 1");

                            t.HasCheckConstraint("ck_routinetask_title_nonempty", "length(title) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.RoutineTaskSchedule", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.PrimitiveCollection<int[]>("DaysOfWeek")
                        .IsRequired()
                        .HasColumnType("integer[]")
                        .HasColumnName("days_of_week");

                    b.Property<int>("Revision")
                        .HasColumnType("integer")
                        .HasColumnName("revision");

                    b.Property<Guid>("RoutineTaskId")
                        .HasColumnType("uuid")
                        .HasColumnName("routine_task_id");

                    b.Property<TimeOnly>("TimeOfDay")
                        .HasColumnType("time without time zone")
                        .HasColumnName("time_of_day");

                    b.Property<string>("TimeZone")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("time_zone");

                    b.HasKey("Id");

                    b.HasIndex("RoutineTaskId", "Revision", "TimeOfDay")
                        .IsUnique();

                    b.ToTable("routine_task_schedule", "routines", t =>
                        {
                            t.HasCheckConstraint("ck_routine_task_schedule_days", "cardinality(days_of_week) BETWEEN 1 AND 7 AND days_of_week <@ ARRAY[1,2,3,4,5,6,7]");

                            t.HasCheckConstraint("ck_routine_task_schedule_revision", "revision >= 1");

                            t.HasCheckConstraint("ck_routinetaskschedule_time_zone_nonempty", "length(time_zone) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.TaskConfirmation", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Decision")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("decision");

                    b.Property<DateTimeOffset>("PerformedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("performed_at");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.Property<Guid>("RecordedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("recorded_by_account_id");

                    b.Property<Guid>("TaskOccurrenceId")
                        .HasColumnType("uuid")
                        .HasColumnName("task_occurrence_id");

                    b.HasKey("Id");

                    b.HasIndex("TaskOccurrenceId")
                        .IsUnique();

                    b.ToTable("task_confirmation", "routines", t =>
                        {
                            t.HasCheckConstraint("ck_task_confirmation_decision", "decision IN ('confirmed','skipped')");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.TaskOccurrence", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("Instructions")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("character varying(2000)")
                        .HasColumnName("instructions");

                    b.Property<DateOnly>("LocalDate")
                        .HasColumnType("date")
                        .HasColumnName("local_date");

                    b.Property<TimeOnly>("LocalTime")
                        .HasColumnType("time without time zone")
                        .HasColumnName("local_time");

                    b.Property<Guid>("RoutineTaskId")
                        .HasColumnType("uuid")
                        .HasColumnName("routine_task_id");

                    b.Property<int>("ScheduleRevision")
                        .HasColumnType("integer")
                        .HasColumnName("schedule_revision");

                    b.Property<DateTimeOffset>("ScheduledAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("scheduled_at");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<string>("TimeZone")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("time_zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("character varying(150)")
                        .HasColumnName("title");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("type");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("SeniorId", "ScheduledAt");

                    b.HasIndex("RoutineTaskId", "ScheduleRevision", "ScheduledAt")
                        .IsUnique();

                    b.HasIndex("RoutineTaskId", "ScheduleRevision", "LocalDate", "LocalTime")
                        .IsUnique();

                    b.ToTable("task_occurrence", "routines", t =>
                        {
                            t.HasCheckConstraint("ck_task_occurrence_revision", "schedule_revision >= 1");

                            t.HasCheckConstraint("ck_task_occurrence_status", "status IN ('pending','confirmed','skipped','missed','cancelled')");

                            t.HasCheckConstraint("ck_task_occurrence_type", "type IN ('medication','meal','hydration')");

                            t.HasCheckConstraint("ck_task_occurrence_version", "version >= 1");

                            t.HasCheckConstraint("ck_taskoccurrence_instructions_nonempty", "length(instructions) >= 1");

                            t.HasCheckConstraint("ck_taskoccurrence_title_nonempty", "length(title) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.RoutineTaskSchedule", b =>
                {
                    b.HasOne("Zpi.Modules.Routines.Domain.RoutineTask", null)
                        .WithMany()
                        .HasForeignKey("RoutineTaskId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.TaskConfirmation", b =>
                {
                    b.HasOne("Zpi.Modules.Routines.Domain.TaskOccurrence", null)
                        .WithMany()
                        .HasForeignKey("TaskOccurrenceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Routines.Domain.TaskOccurrence", b =>
                {
                    b.HasOne("Zpi.Modules.Routines.Domain.RoutineTask", null)
                        .WithMany()
                        .HasForeignKey("RoutineTaskId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
