using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveStopFieldNameFromImportProfileLocator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocatorStopFieldName",
                table: "ImportProfileSheetRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocatorStopFieldName",
                table: "ImportProfileSheetRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            // The stop field was never a free choice in practice (lot 084, G12): restore the one each
            // sheet always used.
            migrationBuilder.Sql(
                "UPDATE ImportProfileSheetRules SET LocatorStopFieldName = " +
                "CASE WHEN SheetName = 'PROCEDURE' THEN 'Action' ELSE 'Identification' END");
        }
    }
}
