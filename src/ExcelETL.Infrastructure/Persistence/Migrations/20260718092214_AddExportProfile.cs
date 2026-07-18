using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExportProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExportProfileSheetRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SheetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PivotSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ExportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfileSheetRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfileSheetRules_ExportProfiles_ExportProfileId",
                        column: x => x.ExportProfileId,
                        principalTable: "ExportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportProfileSheetRuleColumnDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Header = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SheetGenerationRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfileSheetRuleColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfileSheetRuleColumnDefinitions_ExportProfileSheetRules_SheetGenerationRuleId",
                        column: x => x.SheetGenerationRuleId,
                        principalTable: "ExportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExportProfileSheetRulePointColumnDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColonneNom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Header = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MarkValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SheetGenerationRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfileSheetRulePointColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfileSheetRulePointColumnDefinitions_ExportProfileSheetRules_SheetGenerationRuleId",
                        column: x => x.SheetGenerationRuleId,
                        principalTable: "ExportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfileSheetRuleColumnDefinitions_SheetGenerationRuleId",
                table: "ExportProfileSheetRuleColumnDefinitions",
                column: "SheetGenerationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfileSheetRulePointColumnDefinitions_SheetGenerationRuleId",
                table: "ExportProfileSheetRulePointColumnDefinitions",
                column: "SheetGenerationRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfileSheetRules_ExportProfileId",
                table: "ExportProfileSheetRules",
                column: "ExportProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportProfileSheetRuleColumnDefinitions");

            migrationBuilder.DropTable(
                name: "ExportProfileSheetRulePointColumnDefinitions");

            migrationBuilder.DropTable(
                name: "ExportProfileSheetRules");

            migrationBuilder.DropTable(
                name: "ExportProfiles");
        }
    }
}
