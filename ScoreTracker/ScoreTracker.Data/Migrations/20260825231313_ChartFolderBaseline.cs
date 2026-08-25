using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChartFolderBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartFolderBaseline",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Badge = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CoreCutoff = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    QualifiedCount = table.Column<int>(type: "int", nullable: false),
                    AnalyzedCharts = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartFolderBaseline", x => new { x.MixId, x.ChartType, x.Level, x.Badge });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartFolderBaseline",
                schema: "scores");
        }
    }
}
