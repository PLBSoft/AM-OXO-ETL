using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneratedFileRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EquipementRepere = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    SourceFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    TargetFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    TargetFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    ImportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedFileRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedFileRecords_EquipementRepere",
                table: "GeneratedFileRecords",
                column: "EquipementRepere");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedFileRecords_GeneratedAtUtc",
                table: "GeneratedFileRecords",
                column: "GeneratedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedFileRecords");
        }
    }
}
