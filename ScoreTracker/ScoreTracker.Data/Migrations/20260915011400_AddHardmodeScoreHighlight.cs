using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardmodeScoreHighlight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "HardmodeGain",
                schema: "scores",
                table: "ScoreHighlight",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HardmodeRank",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardmodeGain",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "HardmodeRank",
                schema: "scores",
                table: "ScoreHighlight");
        }
    }
}
