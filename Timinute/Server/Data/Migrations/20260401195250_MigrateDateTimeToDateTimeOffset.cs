using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Timinute.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class MigrateDateTimeToDateTimeOffset : Migration
    {
        /// <inheritdoc />
        // Note on UTC preservation for the StartDate/EndDate AlterColumn calls below:
        // SQL Server's implicit conversion of `datetime2` -> `datetimeoffset` copies
        // the date/time components verbatim and tags them with offset `+00:00` (the
        // server timezone is irrelevant). Pre-v2.0 timestamps were already stored
        // as UTC by convention (`DateTime.UtcNow` on the server), so the resulting
        // `DateTimeOffset` instants are correct. If you are restoring from a backup
        // that wrote local-time DateTimes, run a one-off SWITCHOFFSET /
        // TODATETIMEOFFSET adjustment before applying.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceCodes");

            migrationBuilder.DropTable(
                name: "Keys");

            migrationBuilder.DropTable(
                name: "PersistedGrants");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "1dc1392a-cd10-47f4-a25e-768ec5a2fd21");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "d1db9338-44b6-415a-be29-1493394b939e");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "67ca51ee-3cc4-4dae-a65f-b136fcbbf228");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "75e5ad7e-2280-43c0-9a0e-61c5730f8ad1");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "87fdea30-c669-4dc3-b9a1-0c33179ca40e");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "8c671d20-4496-41ec-90b1-5fc3e120c7a7");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "bf5baa4b-d036-4adf-9ff7-c31cb90d983f");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "da2f0f30-eefe-4862-b64a-7e2e9d7864d0");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "fc9de1d0-b03f-470b-8a8f-475c499879c4");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "7c88f8e3-8109-4fd7-a4ab-7d0586ec114e");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "d7053da8-48ca-4efc-8787-cb1fd4df609e");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "e1c0d524-4972-474b-a1da-961cb2aa7afb");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "AspNetRoles");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "StartDate",
                table: "TrackedTasks",
                type: "datetimeoffset",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "EndDate",
                table: "TrackedTasks",
                type: "datetimeoffset",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            // Backfill NULL descriptions before making column non-nullable
            migrationBuilder.Sql("UPDATE AspNetRoles SET Description = '' WHERE Description IS NULL");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "b0a2e199-0a21-4158-8586-b1c2e2a1d64c", "e0c194a8-0001-0001-0001-000000000001", "Basic role with lowest rights.", "Basic", "BASIC" },
                    { "f3c1a2d7-4e5b-4f8a-9c6d-1a2b3c4d5e6f", "e0c194a8-0001-0001-0001-000000000002", "Admin role with highest rights.", "Admin", "ADMIN" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "FirstName", "LastLoginDate", "LastName", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[,]
                {
                    { "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d", 0, "c0c194a8-0001-0001-0001-000000000001", "test1@email.com", true, "Jan", null, "Testovic", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "s0c194a8-0001-0001-0001-000000000001", false, "test1@email.com" },
                    { "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e", 0, "c0c194a8-0001-0001-0001-000000000002", "test2@email.com", true, "Ivana", null, "Maricenkova", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "s0c194a8-0001-0001-0001-000000000002", false, "test2@email.com" },
                    { "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f", 0, "c0c194a8-0001-0001-0001-000000000003", "test3@email.com", true, "Marek", null, "Klukac", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "s0c194a8-0001-0001-0001-000000000003", false, "test3@email.com" }
                });

            migrationBuilder.InsertData(
                table: "TrackedTasks",
                columns: new[] { "TaskId", "Duration", "EndDate", "Name", "ProjectId", "StartDate", "UserId" },
                values: new object[,]
                {
                    { "a7b8c9d0-e1f2-4a5b-8c7d-5e6f7a8b9c0d", new TimeSpan(0, 5, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project D", null, new DateTimeOffset(new DateTime(2022, 2, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e" },
                    { "b8c9d0e1-f2a3-4b5c-8d7e-6f7a8b9c0d1e", new TimeSpan(0, 6, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project E", null, new DateTimeOffset(new DateTime(2022, 1, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e" },
                    { "c9d0e1f2-a3b4-4c5d-8e7f-7a8b9c0d1e2f", new TimeSpan(0, 7, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project F", null, new DateTimeOffset(new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f" },
                    { "d0e1f2a3-b4c5-4d5e-8f7a-8b9c0d1e2f3a", new TimeSpan(0, 7, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project G", null, new DateTimeOffset(new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f" },
                    { "d4e5f6a7-b8c9-4d5e-8f7a-2b3c4d5e6f7a", new TimeSpan(0, 2, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project A", null, new DateTimeOffset(new DateTime(2022, 1, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d" },
                    { "e5f6a7b8-c9d0-4e5f-8a7b-3c4d5e6f7a8b", new TimeSpan(0, 3, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project B", null, new DateTimeOffset(new DateTime(2022, 2, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d" },
                    { "f6a7b8c9-d0e1-4f5a-8b7c-4d5e6f7a8b9c", new TimeSpan(0, 4, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project C", null, new DateTimeOffset(new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "b0a2e199-0a21-4158-8586-b1c2e2a1d64c");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "f3c1a2d7-4e5b-4f8a-9c6d-1a2b3c4d5e6f");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "a7b8c9d0-e1f2-4a5b-8c7d-5e6f7a8b9c0d");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "b8c9d0e1-f2a3-4b5c-8d7e-6f7a8b9c0d1e");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "c9d0e1f2-a3b4-4c5d-8e7f-7a8b9c0d1e2f");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "d0e1f2a3-b4c5-4d5e-8f7a-8b9c0d1e2f3a");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "d4e5f6a7-b8c9-4d5e-8f7a-2b3c4d5e6f7a");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "e5f6a7b8-c9d0-4e5f-8a7b-3c4d5e6f7a8b");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "f6a7b8c9-d0e1-4f5a-8b7c-4d5e6f7a8b9c");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "b2c3d4e5-f6a7-4b5c-8d7e-0f1a2b3c4d5e");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "c3d4e5f6-a7b8-4c5d-8e7f-1a2b3c4d5e6f");

            migrationBuilder.AlterColumn<DateTime>(
                name: "StartDate",
                table: "TrackedTasks",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndDate",
                table: "TrackedTasks",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "datetimeoffset",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "AspNetRoles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "DeviceCodes",
                columns: table => new
                {
                    UserCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceCodes", x => x.UserCode);
                });

            migrationBuilder.CreateTable(
                name: "Keys",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Created = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DataProtected = table.Column<bool>(type: "bit", nullable: false),
                    IsX509Certificate = table.Column<bool>(type: "bit", nullable: false),
                    Use = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersistedGrants",
                columns: table => new
                {
                    Key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ConsumedTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", maxLength: 50000, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Expiration = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SessionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SubjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedGrants", x => x.Key);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Discriminator", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "1dc1392a-cd10-47f4-a25e-768ec5a2fd21", "6b8356a1-68b2-4da4-8234-af799c8aebec", "Admin role with highest rights.", "ApplicationRole", "Admin", "ADMIN" },
                    { "d1db9338-44b6-415a-be29-1493394b939e", "63e8faf0-4100-4ce3-ad3b-73d758f5dc31", "Basic role with lowest rights.", "ApplicationRole", "Basic", "BASIC" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "FirstName", "LastLoginDate", "LastName", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[,]
                {
                    { "7c88f8e3-8109-4fd7-a4ab-7d0586ec114e", 0, "a5da4111-f765-4077-a5b4-3adcf3d7ab03", "test2@email.com", true, "Ivana", null, "Maricenkova", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "a33513ed-b15f-43ed-8b87-e33286e5b124", false, "test2@email.com" },
                    { "d7053da8-48ca-4efc-8787-cb1fd4df609e", 0, "77e9e9b4-3841-4200-805a-e81afd7c84d8", "test3@email.com", true, "Marek", null, "Klukac", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "336e0a2e-5af6-495b-ad1f-fcf11dde9805", false, "test3@email.com" },
                    { "e1c0d524-4972-474b-a1da-961cb2aa7afb", 0, "f49b7918-1bab-4614-91a1-5509197fa95a", "test1@email.com", true, "Jan", null, "Testovic", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "e9d28d12-0bbc-46e3-98bb-ae00bef5c8ba", false, "test1@email.com" }
                });

            migrationBuilder.InsertData(
                table: "TrackedTasks",
                columns: new[] { "TaskId", "Duration", "EndDate", "Name", "ProjectId", "StartDate", "UserId" },
                values: new object[,]
                {
                    { "67ca51ee-3cc4-4dae-a65f-b136fcbbf228", new TimeSpan(0, 6, 0, 0, 0), new DateTime(2022, 1, 1, 19, 0, 0, 0, DateTimeKind.Unspecified), "Project E", null, new DateTime(2022, 1, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), "7c88f8e3-8109-4fd7-a4ab-7d0586ec114e" },
                    { "75e5ad7e-2280-43c0-9a0e-61c5730f8ad1", new TimeSpan(0, 7, 0, 0, 0), new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), "Project G", null, new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), "d7053da8-48ca-4efc-8787-cb1fd4df609e" },
                    { "87fdea30-c669-4dc3-b9a1-0c33179ca40e", new TimeSpan(0, 7, 0, 0, 0), new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), "Project F", null, new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), "d7053da8-48ca-4efc-8787-cb1fd4df609e" },
                    { "8c671d20-4496-41ec-90b1-5fc3e120c7a7", new TimeSpan(0, 3, 0, 0, 0), new DateTime(2022, 2, 2, 13, 0, 0, 0, DateTimeKind.Unspecified), "Project B", null, new DateTime(2022, 2, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), "e1c0d524-4972-474b-a1da-961cb2aa7afb" },
                    { "bf5baa4b-d036-4adf-9ff7-c31cb90d983f", new TimeSpan(0, 2, 0, 0, 0), new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), "Project A", null, new DateTime(2022, 1, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), "e1c0d524-4972-474b-a1da-961cb2aa7afb" },
                    { "da2f0f30-eefe-4862-b64a-7e2e9d7864d0", new TimeSpan(0, 4, 0, 0, 0), new DateTime(2022, 1, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), "Project C", null, new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), "e1c0d524-4972-474b-a1da-961cb2aa7afb" },
                    { "fc9de1d0-b03f-470b-8a8f-475c499879c4", new TimeSpan(0, 5, 0, 0, 0), new DateTime(2022, 2, 2, 17, 0, 0, 0, DateTimeKind.Unspecified), "Project D", null, new DateTime(2022, 2, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), "7c88f8e3-8109-4fd7-a4ab-7d0586ec114e" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCodes_DeviceCode",
                table: "DeviceCodes",
                column: "DeviceCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceCodes_Expiration",
                table: "DeviceCodes",
                column: "Expiration");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_Use",
                table: "Keys",
                column: "Use");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedGrants_ConsumedTime",
                table: "PersistedGrants",
                column: "ConsumedTime");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedGrants_Expiration",
                table: "PersistedGrants",
                column: "Expiration");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedGrants_SubjectId_ClientId_Type",
                table: "PersistedGrants",
                columns: new[] { "SubjectId", "ClientId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedGrants_SubjectId_SessionId_Type",
                table: "PersistedGrants",
                columns: new[] { "SubjectId", "SessionId", "Type" });
        }
    }
}
