using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeaderRulesToImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRuleHeaderComposites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Template = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRuleHeaderComposites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRuleHeaderComposites_ImportProfileSheetRules_SheetExtractionRuleId",
                        column: x => x.SheetExtractionRuleId,
                        principalTable: "ImportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRuleHeaderFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CellSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CellRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StripReperePrefix = table.Column<bool>(type: "bit", nullable: false),
                    DateFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRuleHeaderFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRuleHeaderFields_ImportProfileSheetRules_SheetExtractionRuleId",
                        column: x => x.SheetExtractionRuleId,
                        principalTable: "ImportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRuleHeaderComposites_SheetExtractionRuleId",
                table: "ImportProfileSheetRuleHeaderComposites",
                column: "SheetExtractionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRuleHeaderFields_SheetExtractionRuleId",
                table: "ImportProfileSheetRuleHeaderFields",
                column: "SheetExtractionRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileSheetRuleHeaderComposites");

            migrationBuilder.DropTable(
                name: "ImportProfileSheetRuleHeaderFields");
        }
    }
}
