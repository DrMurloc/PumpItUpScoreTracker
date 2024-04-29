using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorldCompetitive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "UserWorldRanking",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DoublesCompetitive",
                schema: "scores",
                table: "UserWorldRanking",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SinglesCompetitive",
                schema: "scores",
                table: "UserWorldRanking",
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
                table: "UserWorldRanking");

            migrationBuilder.DropColumn(
                name: "DoublesCompetitive",
                schema: "scores",
                table: "UserWorldRanking");

            migrationBuilder.DropColumn(
                name: "SinglesCompetitive",
                schema: "scores",
                table: "UserWorldRanking");
        }
    }
}
