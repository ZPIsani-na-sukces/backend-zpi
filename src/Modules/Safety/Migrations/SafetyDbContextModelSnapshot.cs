using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Zpi.Modules.Safety.Infrastructure.Persistence;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    [DbContext(typeof(SafetyDbContext))]
    partial class SafetyDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("safety")
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

                    b.ToTable("outbox_message", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_outbox_attempts", "attempts >= 0");

                            t.HasCheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.BatterySample", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<int>("BatteryLevelPercent")
                        .HasColumnType("integer")
                        .HasColumnName("battery_level_percent");

                    b.Property<DateTimeOffset>("CapturedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("captured_at");

                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<bool>("IsCharging")
                        .HasColumnType("boolean")
                        .HasColumnName("is_charging");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId", "CapturedAt");

                    b.ToTable("battery_sample", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_battery_sample_battery", "battery_level_percent BETWEEN 0 AND 100");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.Device", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("AppVersion")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("app_version");

                    b.Property<Guid>("AssignedAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("assigned_account_id");

                    b.Property<Guid>("InstallationId")
                        .HasColumnType("uuid")
                        .HasColumnName("installation_id");

                    b.Property<DateTimeOffset?>("LastOnlineAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("last_online_at");

                    b.Property<bool>("MonitoringEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("monitoring_enabled");

                    b.Property<string>("Platform")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("platform");

                    b.Property<DateTimeOffset>("RegisteredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("registered_at");

                    b.Property<DateTimeOffset?>("RetiredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("retired_at");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.HasKey("Id");

                    b.HasIndex("AssignedAccountId")
                        .IsUnique()
                        .HasFilter("monitoring_enabled = true AND retired_at IS NULL");

                    b.HasIndex("InstallationId")
                        .IsUnique();

                    b.ToTable("device", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_device_platform", "platform IN ('android','ios')");

                            t.HasCheckConstraint("ck_device_retired", "retired_at IS NULL OR NOT monitoring_enabled");

                            t.HasCheckConstraint("ck_device_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.GeoZone", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<double>("CenterLatitude")
                        .HasColumnType("double precision")
                        .HasColumnName("center_latitude");

                    b.Property<double>("CenterLongitude")
                        .HasColumnType("double precision")
                        .HasColumnName("center_longitude");

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

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean")
                        .HasColumnName("is_active");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("name");

                    b.Property<double>("RadiusMeters")
                        .HasColumnType("double precision")
                        .HasColumnName("radius_meters");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<bool>("SmsOnExitEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("sms_on_exit_enabled");

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

                    b.ToTable("geo_zone", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_geo_zone_coordinates", "center_latitude BETWEEN -90 AND 90 AND center_longitude BETWEEN -180 AND 180");

                            t.HasCheckConstraint("ck_geo_zone_radius", "radius_meters BETWEEN 50 AND 10000");

                            t.HasCheckConstraint("ck_geo_zone_version", "version >= 1");

                            t.HasCheckConstraint("ck_geozone_name_nonempty", "length(name) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.GeofenceIncident", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<double?>("AccuracyMeters")
                        .HasColumnType("double precision")
                        .HasColumnName("accuracy_meters");

                    b.Property<DateTimeOffset?>("AcknowledgedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("acknowledged_at");

                    b.Property<Guid?>("AcknowledgedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("acknowledged_by_account_id");

                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<Guid>("GeoZoneId")
                        .HasColumnType("uuid")
                        .HasColumnName("geo_zone_id");

                    b.Property<double?>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double?>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<DateTimeOffset?>("PositionCapturedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("position_captured_at");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.Property<string>("ResolutionNote")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("resolution_note");

                    b.Property<DateTimeOffset?>("ResolvedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("resolved_at");

                    b.Property<Guid?>("ResolvedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("resolved_by_account_id");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<double?>("SpeedMps")
                        .HasColumnType("double precision")
                        .HasColumnName("speed_mps");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(40)
                        .HasColumnType("character varying(40)")
                        .HasColumnName("status");

                    b.Property<int>("Version")
                        .IsConcurrencyToken()
                        .HasColumnType("integer")
                        .HasColumnName("version");

                    b.Property<string>("ZoneName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)")
                        .HasColumnName("zone_name");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId");

                    b.HasIndex("GeoZoneId");

                    b.HasIndex("SeniorId", "ReceivedAt");

                    b.ToTable("geofence_incident", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_geofence_incident_accuracy", "accuracy_meters >= 0");

                            t.HasCheckConstraint("ck_geofence_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_geofence_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_geofence_incident_speed", "speed_mps >= 0");

                            t.HasCheckConstraint("ck_geofence_incident_status", "status IN ('open','acknowledged','resolved')");

                            t.HasCheckConstraint("ck_geofence_incident_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.HomeAddress", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<string>("ApartmentNumber")
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)")
                        .HasColumnName("apartment_number");

                    b.Property<string>("BuildingNumber")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)")
                        .HasColumnName("building_number");

                    b.Property<string>("City")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("city");

                    b.Property<string>("CountryCode")
                        .IsRequired()
                        .HasMaxLength(2)
                        .HasColumnType("character varying(2)")
                        .HasColumnName("country_code");

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

                    b.Property<double>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<string>("PostalCode")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("character varying(32)")
                        .HasColumnName("postal_code");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<string>("Street")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)")
                        .HasColumnName("street");

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

                    b.HasIndex("SeniorId")
                        .IsUnique();

                    b.ToTable("home_address", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_home_address_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");

                            t.HasCheckConstraint("ck_home_address_country", "country_code ~ '^[A-Z]{2}$'");

                            t.HasCheckConstraint("ck_home_address_version", "version >= 1");

                            t.HasCheckConstraint("ck_homeaddress_building_number_nonempty", "length(building_number) >= 1");

                            t.HasCheckConstraint("ck_homeaddress_city_nonempty", "length(city) >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.LocationSample", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<double?>("AccuracyMeters")
                        .HasColumnType("double precision")
                        .HasColumnName("accuracy_meters");

                    b.Property<DateTimeOffset>("CapturedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("captured_at");

                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<double>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.Property<double?>("SpeedMps")
                        .HasColumnType("double precision")
                        .HasColumnName("speed_mps");

                    b.HasKey("Id");

                    b.HasIndex("DeviceId", "CapturedAt");

                    b.ToTable("location_sample", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_location_sample_accuracy", "accuracy_meters >= 0");

                            t.HasCheckConstraint("ck_location_sample_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");

                            t.HasCheckConstraint("ck_location_sample_speed", "speed_mps >= 0");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.LowBatteryIncident", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<double?>("AccuracyMeters")
                        .HasColumnType("double precision")
                        .HasColumnName("accuracy_meters");

                    b.Property<DateTimeOffset?>("AcknowledgedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("acknowledged_at");

                    b.Property<Guid?>("AcknowledgedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("acknowledged_by_account_id");

                    b.Property<int>("BatteryLevelPercent")
                        .HasColumnType("integer")
                        .HasColumnName("battery_level_percent");

                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<double?>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double?>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<DateTimeOffset?>("PositionCapturedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("position_captured_at");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.Property<string>("ResolutionNote")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("resolution_note");

                    b.Property<DateTimeOffset?>("ResolvedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("resolved_at");

                    b.Property<Guid?>("ResolvedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("resolved_by_account_id");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<double?>("SpeedMps")
                        .HasColumnType("double precision")
                        .HasColumnName("speed_mps");

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

                    b.HasIndex("DeviceId");

                    b.HasIndex("SeniorId", "ReceivedAt");

                    b.ToTable("low_battery_incident", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_low_battery_incident_accuracy", "accuracy_meters >= 0");

                            t.HasCheckConstraint("ck_low_battery_incident_battery", "battery_level_percent BETWEEN 0 AND 100");

                            t.HasCheckConstraint("ck_low_battery_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_low_battery_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_low_battery_incident_speed", "speed_mps >= 0");

                            t.HasCheckConstraint("ck_low_battery_incident_status", "status IN ('open','acknowledged','resolved')");

                            t.HasCheckConstraint("ck_low_battery_incident_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.SosIncident", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uuid")
                        .HasColumnName("id");

                    b.Property<double?>("AccuracyMeters")
                        .HasColumnType("double precision")
                        .HasColumnName("accuracy_meters");

                    b.Property<DateTimeOffset?>("AcknowledgedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("acknowledged_at");

                    b.Property<Guid?>("AcknowledgedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("acknowledged_by_account_id");

                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<double?>("Latitude")
                        .HasColumnType("double precision")
                        .HasColumnName("latitude");

                    b.Property<double?>("Longitude")
                        .HasColumnType("double precision")
                        .HasColumnName("longitude");

                    b.Property<DateTimeOffset>("OccurredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("occurred_at");

                    b.Property<DateTimeOffset?>("PositionCapturedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("position_captured_at");

                    b.Property<DateTimeOffset>("ReceivedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("received_at");

                    b.Property<string>("ResolutionNote")
                        .HasMaxLength(1000)
                        .HasColumnType("character varying(1000)")
                        .HasColumnName("resolution_note");

                    b.Property<DateTimeOffset?>("ResolvedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("resolved_at");

                    b.Property<Guid?>("ResolvedByAccountId")
                        .HasColumnType("uuid")
                        .HasColumnName("resolved_by_account_id");

                    b.Property<Guid>("SeniorId")
                        .HasColumnType("uuid")
                        .HasColumnName("senior_id");

                    b.Property<double?>("SpeedMps")
                        .HasColumnType("double precision")
                        .HasColumnName("speed_mps");

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

                    b.HasIndex("DeviceId");

                    b.HasIndex("SeniorId")
                        .IsUnique()
                        .HasFilter("status <> 'resolved'");

                    b.HasIndex("SeniorId", "ReceivedAt");

                    b.ToTable("sos_incident", "safety", t =>
                        {
                            t.HasCheckConstraint("ck_sos_incident_accuracy", "accuracy_meters >= 0");

                            t.HasCheckConstraint("ck_sos_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_sos_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");

                            t.HasCheckConstraint("ck_sos_incident_speed", "speed_mps >= 0");

                            t.HasCheckConstraint("ck_sos_incident_status", "status IN ('open','acknowledged','resolved')");

                            t.HasCheckConstraint("ck_sos_incident_version", "version >= 1");
                        });
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.SosTriggerReceipt", b =>
                {
                    b.Property<Guid>("DeviceId")
                        .HasColumnType("uuid")
                        .HasColumnName("device_id");

                    b.Property<DateTimeOffset>("TriggeredAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("triggered_at");

                    b.Property<Guid>("IncidentId")
                        .HasColumnType("uuid")
                        .HasColumnName("incident_id");

                    b.HasKey("DeviceId", "TriggeredAt");

                    b.HasIndex("IncidentId");

                    b.ToTable("sos_trigger_receipt", "safety");
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.BatterySample", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.GeofenceIncident", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Zpi.Modules.Safety.Domain.GeoZone", null)
                        .WithMany()
                        .HasForeignKey("GeoZoneId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.LocationSample", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.LowBatteryIncident", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.SosIncident", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("Zpi.Modules.Safety.Domain.SosTriggerReceipt", b =>
                {
                    b.HasOne("Zpi.Modules.Safety.Domain.Device", null)
                        .WithMany()
                        .HasForeignKey("DeviceId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Zpi.Modules.Safety.Domain.SosIncident", null)
                        .WithMany()
                        .HasForeignKey("IncidentId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
