using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScoreHighlightCompetitiveBaselineAndPumbilityGain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CompetitiveBaseline",
                schema: "scores",
                table: "ScoreHighlight",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PumbilityGain",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompetitiveBaseline",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PumbilityGain",
                schema: "scores",
                table: "ScoreHighlight");
        }
    }
}
