using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyChartsCompetitiveLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "WeeklyUserEntry",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "UserWeeklyPlacing",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "WeeklyUserEntry");

            migrationBuilder.DropColumn(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "UserWeeklyPlacing");
        }
    }
}
