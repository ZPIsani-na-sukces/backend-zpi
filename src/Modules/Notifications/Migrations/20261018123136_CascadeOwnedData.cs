using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zpi.Modules.Notifications.Migrations
{
    public partial class CascadeOwnedData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_delivery_notification_notification_id",
                schema: "notifications",
                table: "notification_delivery");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_delivery_notification_notification_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "notification_id",
                principalSchema: "notifications",
                principalTable: "notification",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notification_delivery_notification_notification_id",
                schema: "notifications",
                table: "notification_delivery");

            migrationBuilder.AddForeignKey(
                name: "FK_notification_delivery_notification_notification_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "notification_id",
                principalSchema: "notifications",
                principalTable: "notification",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
