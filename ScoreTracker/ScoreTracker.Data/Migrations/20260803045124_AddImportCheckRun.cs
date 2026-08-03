using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImportCheckRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportCheckRun",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RanAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OfficialPumbility = table.Column<double>(type: "float", nullable: false),
                    LocalPumbility = table.Column<double>(type: "float", nullable: false),
                    OfficialPasses = table.Column<int>(type: "int", nullable: false),
                    LocalPasses = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportCheckRun", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportCheckRun_UserId_Kind_RanAt",
                schema: "scores",
                table: "ImportCheckRun",
                columns: new[] { "UserId", "Kind", "RanAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportCheckRun_UserId_MixId_RanAt",
                schema: "scores",
                table: "ImportCheckRun",
                columns: new[] { "UserId", "MixId", "RanAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportCheckRun",
                schema: "scores");
        }
    }
}
