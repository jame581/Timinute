using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Timinute.Server.Data.Migrations
{
    public partial class TrackedTaskEndDateOptional : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "113a9386-9a69-414b-9a8a-77446eaadb6e");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "61a0ebb3-22c2-409c-85bb-dd944fc4dcc8");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "TrackedTasks",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "6a608857-2afc-4868-9ebd-54ab3383487f", "a2c382a2-82c5-4a3a-88aa-fcfec1eafab5", "Basic role with lowest rights.", "ApplicationRole", "Basic", "BASIC" });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "5ffecbc3-b8e7-4b0f-bfa7-a6c92045a239", "7799205b-d265-4b0d-b9fd-39769eb21d42", "Admin role with highest rights.", "ApplicationRole", "Admin", "ADMIN" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "5ffecbc3-b8e7-4b0f-bfa7-a6c92045a239");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "6a608857-2afc-4868-9ebd-54ab3383487f");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "TrackedTasks",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "113a9386-9a69-414b-9a8a-77446eaadb6e", "e2ba2eef-f33c-4371-9ea0-cb211b09138a", "Basic role with lowest rights.", "ApplicationRole", "Basic", "BASIC" });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[] { "61a0ebb3-22c2-409c-85bb-dd944fc4dcc8", "33d6b824-08a7-40a0-bee9-83b658b6e395", "Admin role with highest rights.", "ApplicationRole", "Admin", "ADMIN" });
        }
    }
}
