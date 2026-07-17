using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReperePrefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EquipementTypeElementNom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocatorSheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LocatorFirstBlockStartRow = table.Column<int>(type: "int", nullable: false),
                    LocatorStep = table.Column<int>(type: "int", nullable: false),
                    LocatorStopFieldName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UnconditionalColonneNames = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRules_ImportProfiles_ImportProfileId",
                        column: x => x.ImportProfileId,
                        principalTable: "ImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRuleBlockFields",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ColumnRange = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowOffsetStart = table.Column<int>(type: "int", nullable: false),
                    RowOffsetEnd = table.Column<int>(type: "int", nullable: false),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRuleBlockFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRuleBlockFields_ImportProfileSheetRules_SheetExtractionRuleId",
                        column: x => x.SheetExtractionRuleId,
                        principalTable: "ImportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportProfileSheetRulePointRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceFieldName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Operator = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ComparisonValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ColonneName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SheetExtractionRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileSheetRulePointRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileSheetRulePointRules_ImportProfileSheetRules_SheetExtractionRuleId",
                        column: x => x.SheetExtractionRuleId,
                        principalTable: "ImportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRuleBlockFields_SheetExtractionRuleId",
                table: "ImportProfileSheetRuleBlockFields",
                column: "SheetExtractionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRulePointRules_SheetExtractionRuleId",
                table: "ImportProfileSheetRulePointRules",
                column: "SheetExtractionRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileSheetRules_ImportProfileId",
                table: "ImportProfileSheetRules",
                column: "ImportProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileSheetRuleBlockFields");

            migrationBuilder.DropTable(
                name: "ImportProfileSheetRulePointRules");

            migrationBuilder.DropTable(
                name: "ImportProfileSheetRules");

            migrationBuilder.DropTable(
                name: "ImportProfiles");
        }
    }
}
