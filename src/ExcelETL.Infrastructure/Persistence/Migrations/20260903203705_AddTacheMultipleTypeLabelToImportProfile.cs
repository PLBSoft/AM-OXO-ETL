using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExcelETL.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTacheMultipleTypeLabelToImportProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportProfileTacheMultipleTypeLabels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ImportProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportProfileTacheMultipleTypeLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportProfileTacheMultipleTypeLabels_ImportProfiles_ImportProfileId",
                        column: x => x.ImportProfileId,
                        principalTable: "ImportProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportProfileTacheMultipleTypeLabels_ImportProfileId",
                table: "ImportProfileTacheMultipleTypeLabels",
                column: "ImportProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportProfileTacheMultipleTypeLabels");
        }
    }
}
