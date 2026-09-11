using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddElementCountsToGeneratedFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IsolementCount",
                table: "GeneratedFileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PointCount",
                table: "GeneratedFileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TacheMultipleCount",
                table: "GeneratedFileRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsolementCount",
                table: "GeneratedFileRecords");

            migrationBuilder.DropColumn(
                name: "PointCount",
                table: "GeneratedFileRecords");

            migrationBuilder.DropColumn(
                name: "TacheMultipleCount",
                table: "GeneratedFileRecords");
        }
    }
}
