using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWarningsToGeneratedFileRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneratedFileRecordWarnings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Sheet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BlockIdentifier = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ExtractedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GeneratedFileRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedFileRecordWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedFileRecordWarnings_GeneratedFileRecords_GeneratedFileRecordId",
                        column: x => x.GeneratedFileRecordId,
                        principalTable: "GeneratedFileRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedFileRecordWarnings_GeneratedFileRecordId",
                table: "GeneratedFileRecordWarnings",
                column: "GeneratedFileRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedFileRecordWarnings");
        }
    }
}
