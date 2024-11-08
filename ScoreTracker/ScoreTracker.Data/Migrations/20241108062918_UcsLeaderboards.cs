using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class UcsLeaderboards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UcsChart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PiuGameId = table.Column<int>(type: "int", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    SongId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Uploader = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Artist = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UcsChart", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UcsChartLeaderboardEntry",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false),
                    VideoPath = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    ImageUrl = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    RecordedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UcsChartLeaderboardEntry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UcsChart_PiuGameId",
                schema: "scores",
                table: "UcsChart",
                column: "PiuGameId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UcsChartLeaderboardEntry_ChartId_UserId",
                schema: "scores",
                table: "UcsChartLeaderboardEntry",
                columns: new[] { "ChartId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UcsChart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UcsChartLeaderboardEntry",
                schema: "scores");
        }
    }
}
