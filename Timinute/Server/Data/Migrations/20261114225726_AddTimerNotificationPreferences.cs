using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimerNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Preferences_IdleWarningMinutes",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<decimal>(
                name: "Preferences_LongTimerWarningHours",
                table: "AspNetUsers",
                type: "decimal(4,1)",
                nullable: false,
                defaultValue: 8.0m);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d",
                columns: new[] { "Preferences_IdleWarningMinutes", "Preferences_LongTimerWarningHours" },
                values: new object[] { 15, 8.0m });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e",
                columns: new[] { "Preferences_IdleWarningMinutes", "Preferences_LongTimerWarningHours" },
                values: new object[] { 15, 8.0m });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f",
                columns: new[] { "Preferences_IdleWarningMinutes", "Preferences_LongTimerWarningHours" },
                values: new object[] { 15, 8.0m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Preferences_IdleWarningMinutes",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Preferences_LongTimerWarningHours",
                table: "AspNetUsers");
        }
    }
}
