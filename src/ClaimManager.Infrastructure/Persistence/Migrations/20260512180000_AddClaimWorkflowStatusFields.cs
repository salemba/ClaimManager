using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimWorkflowStatusFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blocker_type",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "blocker_reason",
                table: "claims",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "owned_by_user_id",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_expected_action",
                table: "claims",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_data_integrity_warning",
                table: "claims",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "data_integrity_warning_message",
                table: "claims",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "blocker_type",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "blocker_reason",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "owned_by_user_id",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "next_expected_action",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "has_data_integrity_warning",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "data_integrity_warning_message",
                table: "claims");
        }
    }
}