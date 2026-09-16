using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartPumbilityPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartPumbilityPresence",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnOrder = table.Column<int>(type: "int", nullable: false),
                    CountsBoardPlayers = table.Column<bool>(type: "bit", nullable: false),
                    Holders = table.Column<int>(type: "int", nullable: false),
                    SpotMin = table.Column<double>(type: "float", nullable: true),
                    SpotP25 = table.Column<double>(type: "float", nullable: true),
                    SpotMedian = table.Column<double>(type: "float", nullable: true),
                    SpotP75 = table.Column<double>(type: "float", nullable: true),
                    SpotMax = table.Column<double>(type: "float", nullable: true),
                    Dots = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    FolderSpots = table.Column<int>(type: "int", nullable: false),
                    FolderP25 = table.Column<double>(type: "float", nullable: true),
                    FolderMedian = table.Column<double>(type: "float", nullable: true),
                    FolderP75 = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartPumbilityPresence", x => new { x.MixId, x.ChartId, x.ColumnOrder });
                    table.ForeignKey(
                        name: "FK_ChartPumbilityPresence_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChartPumbilityPresenceColumn",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ColumnOrder = table.Column<int>(type: "int", nullable: false),
                    Band = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Players = table.Column<int>(type: "int", nullable: false),
                    SitePlayers = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartPumbilityPresenceColumn", x => new { x.MixId, x.ColumnOrder });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartPumbilityPresence_ChartId",
                schema: "scores",
                table: "ChartPumbilityPresence",
                column: "ChartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartPumbilityPresence",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartPumbilityPresenceColumn",
                schema: "scores");
        }
    }
}
