using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class ExpandLeaderboardData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AverageDifficulty",
                schema: "scores",
                table: "UserTournamentSession",
                type: "float",
                nullable: false,
                defaultValue: 1.0);

            migrationBuilder.AddColumn<int>(
                name: "ChartsPlayed",
                schema: "scores",
                table: "UserTournamentSession",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "RestTime",
                schema: "scores",
                table: "UserTournamentSession",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageDifficulty",
                schema: "scores",
                table: "UserTournamentSession");

            migrationBuilder.DropColumn(
                name: "ChartsPlayed",
                schema: "scores",
                table: "UserTournamentSession");

            migrationBuilder.DropColumn(
                name: "RestTime",
                schema: "scores",
                table: "UserTournamentSession");
        }
    }
}
