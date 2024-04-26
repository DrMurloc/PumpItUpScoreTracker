using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompetitiveLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompetitiveLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DoublesCompetitiveLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SinglesCompetitiveLevel",
                schema: "scores",
                table: "PlayerStats",
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
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "DoublesCompetitiveLevel",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "SinglesCompetitiveLevel",
                schema: "scores",
                table: "PlayerStats");
        }
    }
}
