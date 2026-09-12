using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardmodeBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HardmodeChartsHeld",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "HardmodeDoublesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HardmodeRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HardmodeSinglesRating",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateTable(
                name: "HardmodeChart",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<double>(type: "float", nullable: false),
                    Holders = table.Column<int>(type: "int", nullable: false),
                    FolderSize = table.Column<int>(type: "int", nullable: false),
                    FolderCut = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HardmodeChart", x => new { x.MixId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_HardmodeChart_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfficialHardmodeRating",
                schema: "scores",
                columns: table => new
                {
                    OfficialPlayerId = table.Column<int>(type: "int", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Combined = table.Column<double>(type: "float", nullable: false),
                    Singles = table.Column<double>(type: "float", nullable: false),
                    Doubles = table.Column<double>(type: "float", nullable: false),
                    ChartsHeld = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialHardmodeRating", x => new { x.OfficialPlayerId, x.MixId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_HardmodeChart_ChartId",
                schema: "scores",
                table: "HardmodeChart",
                column: "ChartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HardmodeChart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialHardmodeRating",
                schema: "scores");

            migrationBuilder.DropColumn(
                name: "HardmodeChartsHeld",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "HardmodeDoublesRating",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "HardmodeRating",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "HardmodeSinglesRating",
                schema: "scores",
                table: "PlayerStats");
        }
    }
}
