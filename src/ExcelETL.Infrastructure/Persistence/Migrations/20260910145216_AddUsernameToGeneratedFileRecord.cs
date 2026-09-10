using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToGeneratedFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "GeneratedFileRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                table: "GeneratedFileRecords");
        }
    }
}
