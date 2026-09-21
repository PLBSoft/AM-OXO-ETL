using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantSheetNamesFromImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocatorSheet",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CellSheet",
                table: "ImportProfileSheetRuleHeaderFields");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocatorSheet",
                table: "ImportProfileSheetRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CellSheet",
                table: "ImportProfileSheetRuleHeaderFields",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // Both columns only ever repeated the rule's sheet name (lot 084, G13).
            migrationBuilder.Sql("UPDATE ImportProfileSheetRules SET LocatorSheet = SheetName");
            migrationBuilder.Sql(
                "UPDATE f SET CellSheet = r.SheetName FROM ImportProfileSheetRuleHeaderFields f " +
                "JOIN ImportProfileSheetRules r ON r.Id = f.SheetExtractionRuleId");
        }
    }
}
