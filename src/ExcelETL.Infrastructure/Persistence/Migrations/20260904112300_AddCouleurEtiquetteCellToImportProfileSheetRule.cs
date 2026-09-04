using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCouleurEtiquetteCellToImportProfileSheetRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouleurEtiquetteCellColumnRange",
                table: "ImportProfileSheetRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CouleurEtiquetteCellName",
                table: "ImportProfileSheetRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CouleurEtiquetteCellRowOffsetEnd",
                table: "ImportProfileSheetRules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CouleurEtiquetteCellRowOffsetStart",
                table: "ImportProfileSheetRules",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellColumnRange",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellName",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellRowOffsetEnd",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellRowOffsetStart",
                table: "ImportProfileSheetRules");
        }
    }
}
