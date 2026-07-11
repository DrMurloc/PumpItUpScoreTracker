using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class FolderCohortStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FolderCohortStats",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Bucket = table.Column<int>(type: "int", nullable: false),
                    PassHistogramJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderCohortStats", x => new { x.MixId, x.ChartType, x.Level, x.Bucket });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FolderCohortStats",
                schema: "scores");
        }
    }
}
