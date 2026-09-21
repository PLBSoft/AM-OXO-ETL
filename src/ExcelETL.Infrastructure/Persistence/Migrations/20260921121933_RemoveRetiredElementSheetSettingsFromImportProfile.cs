using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRetiredElementSheetSettingsFromImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileSheetRuleFieldPresencePointRules");

            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellColumnRange",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellIsRequired",
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

            migrationBuilder.DropColumn(
                name: "ZeroEnergieExpectedValue",
                table: "ImportProfileSheetRules");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouleurEtiquetteCellColumnRange",
                table: "ImportProfileSheetRules",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CouleurEtiquetteCellIsRequired",
                table: "ImportProfileSheetRules",
                type: "bit",
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

            migrationBuilder.AddColumn<string>(
                name: "ZeroEnergieExpectedValue",
                table: "ImportProfileSheetRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRuleFieldPresencePointRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColonneName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpectedValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false),
                    CellColumnRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CellIsRequired = table.Column<bool>(type: "bit", nullable: false),
                    CellName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CellRowOffsetEnd = table.Column<int>(type: "int", nullable: false),
                    CellRowOffsetStart = table.Column<int>(type: "int", nullable: false)
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
    }
}
