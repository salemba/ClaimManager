using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPolicySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "coverage_type",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "policy_effective_date",
                table: "claims",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "policy_expiration_date",
                table: "claims",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "policy_holder",
                table: "claims",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "policy_synced_at_utc",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                columns: new[] { "coverage_type", "policy_effective_date", "policy_expiration_date", "policy_holder", "policy_synced_at_utc" },
                values: new object[] { null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "coverage_type",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "policy_effective_date",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "policy_expiration_date",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "policy_holder",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "policy_synced_at_utc",
                table: "claims");
        }
    }
}
