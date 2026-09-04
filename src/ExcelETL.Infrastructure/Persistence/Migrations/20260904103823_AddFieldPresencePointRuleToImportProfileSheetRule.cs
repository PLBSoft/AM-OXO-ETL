using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFieldPresencePointRuleToImportProfileSheetRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRuleFieldPresencePointRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CellName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CellColumnRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CellRowOffsetStart = table.Column<int>(type: "int", nullable: false),
                    CellRowOffsetEnd = table.Column<int>(type: "int", nullable: false),
                    ColonneName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRuleFieldPresencePointRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRuleFieldPresencePointRules_ImportProfileSheetRules_SheetExtractionRuleId",
                        column: x => x.SheetExtractionRuleId,
                        principalTable: "ImportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRuleFieldPresencePointRules_SheetExtractionRuleId",
                table: "ImportProfileSheetRuleFieldPresencePointRules",
                column: "SheetExtractionRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileSheetRuleFieldPresencePointRules");
        }
    }
}
