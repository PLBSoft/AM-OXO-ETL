using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FilesPurgedAtUtc",
                table: "GeneratedFileRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchiveRetentionSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    RetentionDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveRetentionSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchiveRetentionSettings");

            migrationBuilder.DropColumn(
                name: "FilesPurgedAtUtc",
                table: "GeneratedFileRecords");
        }
    }
}
