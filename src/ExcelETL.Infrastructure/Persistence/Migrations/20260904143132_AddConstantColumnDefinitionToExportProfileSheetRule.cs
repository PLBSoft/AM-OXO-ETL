using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConstantColumnDefinitionToExportProfileSheetRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportProfileSheetRuleConstantColumnDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Header = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SheetGenerationRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfileSheetRuleConstantColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfileSheetRuleConstantColumnDefinitions_ExportProfileSheetRules_SheetGenerationRuleId",
                        column: x => x.SheetGenerationRuleId,
                        principalTable: "ExportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfileSheetRuleConstantColumnDefinitions_SheetGenerationRuleId",
                table: "ExportProfileSheetRuleConstantColumnDefinitions",
                column: "SheetGenerationRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportProfileSheetRuleConstantColumnDefinitions");
        }
    }
}
