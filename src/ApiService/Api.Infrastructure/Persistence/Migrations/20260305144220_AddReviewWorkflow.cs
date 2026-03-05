using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "extracted_fields",
                table: "documents",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reviewed_at",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reviewed_by",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    performed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    field_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    previous_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_tenant_document",
                table: "audit_log",
                columns: new[] { "tenant_id", "document_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropColumn(
                name: "extracted_fields",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "reviewed_at",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "reviewed_by",
                table: "documents");
        }
    }
}
