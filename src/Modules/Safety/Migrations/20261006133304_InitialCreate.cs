using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Safety.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "safety");

            migrationBuilder.CreateTable(
                name: "device",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    installation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    app_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    monitoring_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    registered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_online_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device", x => x.id);
                    table.CheckConstraint("ck_device_platform", "platform IN ('android','ios')");
                    table.CheckConstraint("ck_device_retired", "retired_at IS NULL OR NOT monitoring_enabled");
                    table.CheckConstraint("ck_device_version", "version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "geo_zone",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    center_latitude = table.Column<double>(type: "double precision", nullable: false),
                    center_longitude = table.Column<double>(type: "double precision", nullable: false),
                    radius_meters = table.Column<double>(type: "double precision", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sms_on_exit_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geo_zone", x => x.id);
                    table.CheckConstraint("ck_geo_zone_coordinates", "center_latitude BETWEEN -90 AND 90 AND center_longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_geo_zone_radius", "radius_meters BETWEEN 50 AND 10000");
                    table.CheckConstraint("ck_geo_zone_version", "version >= 1");
                    table.CheckConstraint("ck_geozone_name_nonempty", "length(name) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "home_address",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    city = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    street = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    building_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    apartment_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    created_by_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_home_address", x => x.id);
                    table.CheckConstraint("ck_home_address_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_home_address_country", "country_code ~ '^[A-Z]{2}$'");
                    table.CheckConstraint("ck_home_address_version", "version >= 1");
                    table.CheckConstraint("ck_homeaddress_building_number_nonempty", "length(building_number) >= 1");
                    table.CheckConstraint("ck_homeaddress_city_nonempty", "length(city) >= 1");
                });

            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    contract_version = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_message", x => x.id);
                    table.CheckConstraint("ck_outbox_attempts", "attempts >= 0");
                    table.CheckConstraint("ck_outbox_contract_version", "contract_version >= 1");
                });

            migrationBuilder.CreateTable(
                name: "battery_sample",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    battery_level_percent = table.Column<int>(type: "integer", nullable: false),
                    is_charging = table.Column<bool>(type: "boolean", nullable: false),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_battery_sample", x => x.id);
                    table.CheckConstraint("ck_battery_sample_battery", "battery_level_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_battery_sample_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_sample",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    speed_mps = table.Column<double>(type: "double precision", nullable: true),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_sample", x => x.id);
                    table.CheckConstraint("ck_location_sample_accuracy", "accuracy_meters >= 0");
                    table.CheckConstraint("ck_location_sample_coordinates", "latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_location_sample_speed", "speed_mps >= 0");
                    table.ForeignKey(
                        name: "FK_location_sample_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "low_battery_incident",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    speed_mps = table.Column<double>(type: "double precision", nullable: true),
                    position_captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    battery_level_percent = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_low_battery_incident", x => x.id);
                    table.CheckConstraint("ck_low_battery_incident_accuracy", "accuracy_meters >= 0");
                    table.CheckConstraint("ck_low_battery_incident_battery", "battery_level_percent BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_low_battery_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");
                    table.CheckConstraint("ck_low_battery_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_low_battery_incident_speed", "speed_mps >= 0");
                    table.CheckConstraint("ck_low_battery_incident_status", "status IN ('open','acknowledged','resolved')");
                    table.CheckConstraint("ck_low_battery_incident_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_low_battery_incident_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sos_incident",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    speed_mps = table.Column<double>(type: "double precision", nullable: true),
                    position_captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sos_incident", x => x.id);
                    table.CheckConstraint("ck_sos_incident_accuracy", "accuracy_meters >= 0");
                    table.CheckConstraint("ck_sos_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");
                    table.CheckConstraint("ck_sos_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_sos_incident_speed", "speed_mps >= 0");
                    table.CheckConstraint("ck_sos_incident_status", "status IN ('open','acknowledged','resolved')");
                    table.CheckConstraint("ck_sos_incident_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_sos_incident_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "geofence_incident",
                schema: "safety",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    senior_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    accuracy_meters = table.Column<double>(type: "double precision", nullable: true),
                    speed_mps = table.Column<double>(type: "double precision", nullable: true),
                    position_captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolution_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    geo_zone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    zone_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_geofence_incident", x => x.id);
                    table.CheckConstraint("ck_geofence_incident_accuracy", "accuracy_meters >= 0");
                    table.CheckConstraint("ck_geofence_incident_position", "(latitude IS NULL AND longitude IS NULL AND position_captured_at IS NULL AND accuracy_meters IS NULL AND speed_mps IS NULL) OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180 AND latitude IS NOT NULL AND longitude IS NOT NULL AND position_captured_at IS NOT NULL)");
                    table.CheckConstraint("ck_geofence_incident_reaction", "(status = 'open' AND acknowledged_at IS NULL AND resolved_at IS NULL) OR (status = 'acknowledged' AND acknowledged_at IS NOT NULL AND resolved_at IS NULL) OR (status = 'resolved' AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_geofence_incident_speed", "speed_mps >= 0");
                    table.CheckConstraint("ck_geofence_incident_status", "status IN ('open','acknowledged','resolved')");
                    table.CheckConstraint("ck_geofence_incident_version", "version >= 1");
                    table.ForeignKey(
                        name: "FK_geofence_incident_device_device_id",
                        column: x => x.device_id,
                        principalSchema: "safety",
                        principalTable: "device",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_geofence_incident_geo_zone_geo_zone_id",
                        column: x => x.geo_zone_id,
                        principalSchema: "safety",
                        principalTable: "geo_zone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_battery_sample_device_id_captured_at",
                schema: "safety",
                table: "battery_sample",
                columns: new[] { "device_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_device_assigned_account_id",
                schema: "safety",
                table: "device",
                column: "assigned_account_id",
                unique: true,
                filter: "monitoring_enabled = true AND retired_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_device_installation_id",
                schema: "safety",
                table: "device",
                column: "installation_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_geo_zone_senior_id",
                schema: "safety",
                table: "geo_zone",
                column: "senior_id");

            migrationBuilder.CreateIndex(
                name: "IX_geofence_incident_device_id",
                schema: "safety",
                table: "geofence_incident",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_geofence_incident_geo_zone_id",
                schema: "safety",
                table: "geofence_incident",
                column: "geo_zone_id");

            migrationBuilder.CreateIndex(
                name: "IX_geofence_incident_senior_id_received_at",
                schema: "safety",
                table: "geofence_incident",
                columns: new[] { "senior_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_home_address_senior_id",
                schema: "safety",
                table: "home_address",
                column: "senior_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_location_sample_device_id_captured_at",
                schema: "safety",
                table: "location_sample",
                columns: new[] { "device_id", "captured_at" });

            migrationBuilder.CreateIndex(
                name: "IX_low_battery_incident_device_id",
                schema: "safety",
                table: "low_battery_incident",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_low_battery_incident_senior_id_received_at",
                schema: "safety",
                table: "low_battery_incident",
                columns: new[] { "senior_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_message_next_attempt_at_occurred_at",
                schema: "safety",
                table: "outbox_message",
                columns: new[] { "next_attempt_at", "occurred_at" },
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sos_incident_device_id",
                schema: "safety",
                table: "sos_incident",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_sos_incident_senior_id_received_at",
                schema: "safety",
                table: "sos_incident",
                columns: new[] { "senior_id", "received_at" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "battery_sample",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "geofence_incident",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "home_address",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "location_sample",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "low_battery_incident",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "sos_incident",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "geo_zone",
                schema: "safety");

            migrationBuilder.DropTable(
                name: "device",
                schema: "safety");
        }
    }
}
