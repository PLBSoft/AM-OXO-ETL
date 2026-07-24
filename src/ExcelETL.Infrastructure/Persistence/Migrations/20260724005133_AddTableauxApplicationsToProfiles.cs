using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTableauxApplicationsToProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultApplicationNames",
                table: "ImportProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "DefaultTableaux",
                table: "ImportProfiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "ExportProfileSheetRuleApplicationColumnDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationNom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Header = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MarkValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SheetGenerationRuleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportProfileSheetRuleApplicationColumnDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExportProfileSheetRuleApplicationColumnDefinitions_ExportProfileSheetRules_SheetGenerationRuleId",
                        column: x => x.SheetGenerationRuleId,
                        principalTable: "ExportProfileSheetRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExportProfileSheetRuleApplicationColumnDefinitions_SheetGenerationRuleId",
                table: "ExportProfileSheetRuleApplicationColumnDefinitions",
                column: "SheetGenerationRuleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportProfileSheetRuleApplicationColumnDefinitions");

            migrationBuilder.DropColumn(
                name: "DefaultApplicationNames",
                table: "ImportProfiles");

            migrationBuilder.DropColumn(
                name: "DefaultTableaux",
                table: "ImportProfiles");
        }
    }
}
