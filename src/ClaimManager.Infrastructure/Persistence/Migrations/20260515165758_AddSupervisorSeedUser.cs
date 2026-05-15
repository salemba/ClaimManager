using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorSeedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "access_failed_count", "concurrency_stamp", "created_at_utc", "email", "email_confirmed", "lockout_enabled", "lockout_end", "normalized_email", "normalized_user_name", "password_hash", "phone_number", "phone_number_confirmed", "security_stamp", "two_factor_enabled", "user_name" },
                values: new object[] { new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"), 0, "user-dddddddd-dddd-dddd-dddd-dddddddddddd", new DateTime(2026, 5, 11, 0, 0, 0, 0, DateTimeKind.Utc), "supervisor@claimmanager.local", true, false, null, "SUPERVISOR@CLAIMMANAGER.LOCAL", "SUPERVISOR@CLAIMMANAGER.LOCAL", "AQAAAAIAAYagAAAAEOUuFEwF4KN1J99pZOcA/NhSulx4W7w+YJV/mk8bRg6lI2sxfCR7TK2uiq/i95CuMA==", null, false, "security-dddddddd-dddd-dddd-dddd-dddddddddddd", false, "supervisor@claimmanager.local" });

            migrationBuilder.InsertData(
                table: "user_roles",
                columns: new[] { "role_id", "user_id" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "user_roles",
                keyColumns: new[] { "role_id", "user_id" },
                keyValues: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd") });

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("dddddddd-dddd-dddd-dddd-dddddddddddd"));
        }
    }
}
