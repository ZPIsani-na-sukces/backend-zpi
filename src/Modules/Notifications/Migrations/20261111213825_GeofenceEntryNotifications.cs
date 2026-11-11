using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    public partial class GeofenceEntryNotifications : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_target_kind",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_target_type",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_type",
                schema: "notifications",
                table: "notification");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_target_kind",
                schema: "notifications",
                table: "notification",
                sql: "target_kind IN ('incident','taskOccurrence','careInvitation','geoZone')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_target_type",
                schema: "notifications",
                table: "notification",
                sql: "(type IN ('sos','geofenceExit','lowBattery') AND target_kind = 'incident') OR (type = 'geofenceEntry' AND target_kind = 'geoZone') OR (type = 'routineReminder' AND target_kind = 'taskOccurrence') OR (type = 'careInvitation' AND target_kind = 'careInvitation')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_type",
                schema: "notifications",
                table: "notification",
                sql: "type IN ('sos','geofenceExit','lowBattery','routineReminder','careInvitation','geofenceEntry')");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_target_kind",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_target_type",
                schema: "notifications",
                table: "notification");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notification_type",
                schema: "notifications",
                table: "notification");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_target_kind",
                schema: "notifications",
                table: "notification",
                sql: "target_kind IN ('incident','taskOccurrence','careInvitation')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_target_type",
                schema: "notifications",
                table: "notification",
                sql: "(type IN ('sos','geofenceExit','lowBattery') AND target_kind = 'incident') OR (type = 'routineReminder' AND target_kind = 'taskOccurrence') OR (type = 'careInvitation' AND target_kind = 'careInvitation')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notification_type",
                schema: "notifications",
                table: "notification",
                sql: "type IN ('sos','geofenceExit','lowBattery','routineReminder','careInvitation')");
        }
    }
}
