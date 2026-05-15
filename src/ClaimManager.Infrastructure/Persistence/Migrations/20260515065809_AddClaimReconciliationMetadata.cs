using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimReconciliationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "active_data_integrity_issues_json",
                table: "claims",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_reconciliation_details_json",
                table: "claims",
                type: "jsonb",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                columns: new[] { "active_data_integrity_issues_json", "last_reconciliation_details_json" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "active_data_integrity_issues_json",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "last_reconciliation_details_json",
                table: "claims");
        }
    }
}
