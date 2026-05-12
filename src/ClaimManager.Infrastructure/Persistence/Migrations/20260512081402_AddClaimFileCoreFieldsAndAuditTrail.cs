using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimFileCoreFieldsAndAuditTrail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "claimant_email",
                table: "claims",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "claimant_name",
                table: "claims",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "claimant_phone",
                table: "claims",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "created_by_user_id",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "loss_date_utc",
                table: "claims",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "loss_description",
                table: "claims",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "loss_type",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "policy_number",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by_user_id",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "claim_audits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    performed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    performed_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_audits", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_audits_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                columns: new[] { "claimant_email", "claimant_name", "claimant_phone", "created_by_user_id", "loss_date_utc", "loss_description", "loss_type", "policy_number", "updated_at_utc", "updated_by_user_id" },
                values: new object[] { "jordan.avery@example.com", "Jordan Avery", "555-0100", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", new DateTime(2026, 5, 8, 0, 0, 0, 0, DateTimeKind.Utc), "Kitchen pipe burst caused water damage across the lower level.", "Water damage", "POL-2026-0001", null, null });

            migrationBuilder.CreateIndex(
                name: "ix_claim_audits_claim_id_performed_at_utc",
                table: "claim_audits",
                columns: new[] { "claim_id", "performed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_audits");

            migrationBuilder.DropColumn(
                name: "claimant_email",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "claimant_name",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "claimant_phone",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "created_by_user_id",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "loss_date_utc",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "loss_description",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "loss_type",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "policy_number",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "updated_by_user_id",
                table: "claims");
        }
    }
}
