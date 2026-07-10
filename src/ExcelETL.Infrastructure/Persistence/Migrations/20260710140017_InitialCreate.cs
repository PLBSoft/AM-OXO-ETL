using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExtractionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFilePath = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SheetConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SheetName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SheetIndex = table.Column<int>(type: "int", nullable: false),
                    ExtractionConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetConfigs_ExtractionConfigs_ExtractionConfigId",
                        column: x => x.ExtractionConfigId,
                        principalTable: "ExtractionConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CellMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceCell = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetPropertyName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SheetConfigId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CellMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CellMappings_SheetConfigs_SheetConfigId",
                        column: x => x.SheetConfigId,
                        principalTable: "SheetConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CellMappings_SheetConfigId",
                table: "CellMappings",
                column: "SheetConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_SheetConfigs_ExtractionConfigId",
                table: "SheetConfigs",
                column: "ExtractionConfigId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CellMappings");

            migrationBuilder.DropTable(
                name: "ExtractionHistories");

            migrationBuilder.DropTable(
                name: "SheetConfigs");

            migrationBuilder.DropTable(
                name: "ExtractionConfigs");
        }
    }
}
