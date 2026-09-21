using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElementSheetSettingsToImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CouleurEtiquetteCellIsRequired",
                table: "ImportProfileSheetRules",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WarnWhenNoConditionalPoint",
                table: "ImportProfileSheetRules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "ComparisonValue",
                table: "ImportProfileSheetRulePointRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            // Lot 084: every field saved before the lot stays required (BlockFieldDefinition's own
            // default) -- hand-edited from the scaffolded "false".
            migrationBuilder.AddColumn<bool>(
                name: "CellIsRequired",
                table: "ImportProfileSheetRuleFieldPresencePointRules",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "ImportProfileSheetRuleBlockFields",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // The couleur cell is an optional owned type split onto the rule row: a configured cell
            // must get a value, an absent one stays null.
            migrationBuilder.Sql(
                "UPDATE ImportProfileSheetRules SET CouleurEtiquetteCellIsRequired = 1 " +
                "WHERE CouleurEtiquetteCellName IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CouleurEtiquetteCellIsRequired",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "WarnWhenNoConditionalPoint",
                table: "ImportProfileSheetRules");

            migrationBuilder.DropColumn(
                name: "CellIsRequired",
                table: "ImportProfileSheetRuleFieldPresencePointRules");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "ImportProfileSheetRuleBlockFields");

            migrationBuilder.AlterColumn<string>(
                name: "ComparisonValue",
                table: "ImportProfileSheetRulePointRules",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
