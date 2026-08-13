using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PumbilityCensus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PumbilityCensusEntry",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    CohortKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Appearances = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PumbilityCensusEntry", x => new { x.MixId, x.ChartType, x.Level, x.CohortKey, x.ChartId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PumbilityCensusEntry",
                schema: "scores");
        }
    }
}
