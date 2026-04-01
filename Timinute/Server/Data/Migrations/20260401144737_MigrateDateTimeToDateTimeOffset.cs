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
                    { "85863c97-b2e4-4164-a73e-ba5afa746343", "0846ebe0-99df-4d35-a503-7b4908078710", "Basic role with lowest rights.", "Basic", "BASIC" },
                    { "a95f936c-7513-4ceb-9264-ecac8163b7ee", "c1faf1ec-02dd-4fa4-9064-8d1a00900d35", "Admin role with highest rights.", "Admin", "ADMIN" }
                });

            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "Email", "EmailConfirmed", "FirstName", "LastLoginDate", "LastName", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[,]
                {
                    { "6c1708b1-fa09-491f-8191-0893380cf8e7", 0, "5d9efd91-2a62-4faa-a252-2acd16ea0336", "test3@email.com", true, "Marek", null, "Klukac", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "0217e6c4-3126-48af-a0b0-c9de8aca04ba", false, "test3@email.com" },
                    { "73876802-544e-4290-a3f2-850419d38c1d", 0, "1ed1b897-7fd6-42ad-ad9d-9c105daae108", "test1@email.com", true, "Jan", null, "Testovic", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "9bd3ee4a-712f-4e61-a003-312d52a7e719", false, "test1@email.com" },
                    { "d0a44264-40bf-46d9-a39f-0a7e4f0d68ec", 0, "692c80c8-b1cf-4431-be2f-9589d4d2077f", "test2@email.com", true, "Ivana", null, "Maricenkova", false, null, null, null, "AQAAAAEAACcQAAAAEDgV3QGcSGxXfgIEFYvljstwmQb05lu59FQY/6H4R7SLAZkYc2uJCmNyio51dtfuGg==", null, false, "58da6096-a3ae-49fc-b45b-ae4d45c5b2d2", false, "test2@email.com" }
                });

            migrationBuilder.InsertData(
                table: "TrackedTasks",
                columns: new[] { "TaskId", "Duration", "EndDate", "Name", "ProjectId", "StartDate", "UserId" },
                values: new object[,]
                {
                    { "02d034ef-5fcd-47cf-8da1-d003a39057d4", new TimeSpan(0, 2, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project A", null, new DateTimeOffset(new DateTime(2022, 1, 1, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "73876802-544e-4290-a3f2-850419d38c1d" },
                    { "1eb23001-631c-4033-9207-1957bc9d361a", new TimeSpan(0, 3, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project B", null, new DateTimeOffset(new DateTime(2022, 2, 2, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "73876802-544e-4290-a3f2-850419d38c1d" },
                    { "3dbd0361-41fa-48ce-a7b7-131066dd9be3", new TimeSpan(0, 4, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 15, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project C", null, new DateTimeOffset(new DateTime(2022, 1, 1, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "73876802-544e-4290-a3f2-850419d38c1d" },
                    { "c8029768-3ed4-483a-8620-96022dce5241", new TimeSpan(0, 7, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project F", null, new DateTimeOffset(new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "6c1708b1-fa09-491f-8191-0893380cf8e7" },
                    { "cb741e1a-b452-41b1-bd2f-f62e1cc5369c", new TimeSpan(0, 5, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project D", null, new DateTimeOffset(new DateTime(2022, 2, 2, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "d0a44264-40bf-46d9-a39f-0a7e4f0d68ec" },
                    { "e96e388b-7bcb-46af-bc01-9a32ff3b9428", new TimeSpan(0, 6, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 1, 1, 19, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project E", null, new DateTimeOffset(new DateTime(2022, 1, 1, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "d0a44264-40bf-46d9-a39f-0a7e4f0d68ec" },
                    { "f32b3016-4882-40b5-91a4-625793a3b6a2", new TimeSpan(0, 7, 0, 0, 0), new DateTimeOffset(new DateTime(2022, 2, 2, 21, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Project G", null, new DateTimeOffset(new DateTime(2022, 2, 2, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "6c1708b1-fa09-491f-8191-0893380cf8e7" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "85863c97-b2e4-4164-a73e-ba5afa746343");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a95f936c-7513-4ceb-9264-ecac8163b7ee");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "02d034ef-5fcd-47cf-8da1-d003a39057d4");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "1eb23001-631c-4033-9207-1957bc9d361a");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "3dbd0361-41fa-48ce-a7b7-131066dd9be3");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "c8029768-3ed4-483a-8620-96022dce5241");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "cb741e1a-b452-41b1-bd2f-f62e1cc5369c");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "e96e388b-7bcb-46af-bc01-9a32ff3b9428");

            migrationBuilder.DeleteData(
                table: "TrackedTasks",
                keyColumn: "TaskId",
                keyValue: "f32b3016-4882-40b5-91a4-625793a3b6a2");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "6c1708b1-fa09-491f-8191-0893380cf8e7");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "73876802-544e-4290-a3f2-850419d38c1d");

            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "d0a44264-40bf-46d9-a39f-0a7e4f0d68ec");

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
