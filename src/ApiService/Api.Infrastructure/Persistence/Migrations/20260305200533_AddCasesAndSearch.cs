using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCasesAndSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "case_id",
                table: "documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_case_id",
                table: "documents",
                column: "case_id");

            migrationBuilder.CreateIndex(
                name: "ix_cases_tenant_subject_name",
                table: "cases",
                columns: new[] { "tenant_id", "subject_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_documents_cases_case_id",
                table: "documents",
                column: "case_id",
                principalTable: "cases",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_documents_cases_case_id",
                table: "documents");

            migrationBuilder.DropTable(
                name: "cases");

            migrationBuilder.DropIndex(
                name: "IX_documents_case_id",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "case_id",
                table: "documents");
        }
    }
}
