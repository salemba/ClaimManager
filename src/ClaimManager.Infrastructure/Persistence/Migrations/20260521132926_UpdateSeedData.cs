using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "claims",
                columns: new[] { "id", "active_data_integrity_issues_json", "blocker_reason", "blocker_type", "claim_number", "claimant_email", "claimant_name", "claimant_phone", "coverage_type", "created_at_utc", "created_by_user_id", "data_integrity_warning_message", "document_synced_at_utc", "last_reconciliation_details_json", "loss_date_utc", "loss_description", "loss_type", "next_expected_action", "owned_by_user_id", "payment_amount", "payment_currency", "payment_reference", "payment_settled_at", "payment_status", "payment_synced_at_utc", "policy_effective_date", "policy_expiration_date", "policy_holder", "policy_number", "policy_synced_at_utc", "status", "updated_at_utc", "updated_by_user_id" },
                values: new object[,]
                {
                    { new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), null, "High value settlement requires senior sign-off.", "awaiting-payment-approval", "CLM-0002", "terry.smith@example.com", "Terry Smith", "555-0101", null, new DateTime(2026, 5, 10, 0, 0, 0, 0, DateTimeKind.Utc), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", null, null, null, new DateTime(2026, 5, 6, 0, 0, 0, 0, DateTimeKind.Utc), "Two-car collision at intersection.", "Collision", null, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", null, null, null, null, null, null, null, null, null, "POL-2026-0002", null, "active", null, null },
                    { new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"), null, null, null, "CLM-0003", "alex.rivera@example.com", "Alex Rivera", "555-0102", null, new DateTime(2026, 4, 21, 0, 0, 0, 0, DateTimeKind.Utc), "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", null, null, null, new DateTime(2026, 4, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Vehicle stolen from driveway.", "Theft", null, "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", null, null, null, null, null, null, null, null, null, "POL-2026-0003", null, "active", null, null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"));

            migrationBuilder.DeleteData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        }
    }
}
