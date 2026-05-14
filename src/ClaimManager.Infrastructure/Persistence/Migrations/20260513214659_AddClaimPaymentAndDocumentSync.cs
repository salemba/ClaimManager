using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimPaymentAndDocumentSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "document_synced_at_utc",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "payment_amount",
                table: "claims",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_currency",
                table: "claims",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_reference",
                table: "claims",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payment_settled_at",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payment_status",
                table: "claims",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_synced_at_utc",
                table: "claims",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "claim_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "uploaded");

            migrationBuilder.UpdateData(
                table: "claims",
                keyColumn: "id",
                keyValue: new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                columns: new[] { "document_synced_at_utc", "payment_amount", "payment_currency", "payment_reference", "payment_settled_at", "payment_status", "payment_synced_at_utc" },
                values: new object[] { null, null, null, null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_synced_at_utc",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_amount",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_currency",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_reference",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_settled_at",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_status",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "payment_synced_at_utc",
                table: "claims");

            migrationBuilder.DropColumn(
                name: "source",
                table: "claim_documents");
        }
    }
}
