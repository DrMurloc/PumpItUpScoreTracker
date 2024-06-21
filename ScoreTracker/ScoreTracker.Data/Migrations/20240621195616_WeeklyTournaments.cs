using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyTournaments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PastTourneyCharts",
                schema: "scores",
                columns: table => new
                {
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PastTourneyCharts", x => x.ChartId);
                });

            migrationBuilder.CreateTable(
                name: "UserWeeklyPlacing",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false),
                    ObtainedDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WasWithinRange = table.Column<bool>(type: "bit", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWeeklyPlacing", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyTournamentChart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpirationDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyTournamentChart", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WeeklyUserEntry",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false),
                    WasWithinRange = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyUserEntry", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWeeklyPlacing_UserId",
                schema: "scores",
                table: "UserWeeklyPlacing",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyUserEntry_UserId_ChartId",
                schema: "scores",
                table: "WeeklyUserEntry",
                columns: new[] { "UserId", "ChartId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PastTourneyCharts",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserWeeklyPlacing",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "WeeklyTournamentChart",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "WeeklyUserEntry",
                schema: "scores");
        }
    }
}
