using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClaimManager.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimNotesAndDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_identifier = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    uploaded_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_documents_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "claim_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_notes", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_notes_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_documents_claim_id_uploaded_at_utc",
                table: "claim_documents",
                columns: new[] { "claim_id", "uploaded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_claim_documents_storage_identifier",
                table: "claim_documents",
                column: "storage_identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_claim_notes_claim_id_created_at_utc",
                table: "claim_notes",
                columns: new[] { "claim_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_documents");

            migrationBuilder.DropTable(
                name: "claim_notes");
        }
    }
}
