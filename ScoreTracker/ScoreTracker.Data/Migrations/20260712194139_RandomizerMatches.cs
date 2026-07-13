using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class RandomizerMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RandomizerDraw_TournamentId",
                schema: "scores",
                table: "RandomizerDraw");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "scores",
                table: "RandomizerDraw",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDraw_TournamentId",
                schema: "scores",
                table: "RandomizerDraw",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RandomizerDraw_TournamentId",
                schema: "scores",
                table: "RandomizerDraw");

            migrationBuilder.DropColumn(
                name: "Name",
                schema: "scores",
                table: "RandomizerDraw");

            migrationBuilder.CreateIndex(
                name: "IX_RandomizerDraw_TournamentId",
                schema: "scores",
                table: "RandomizerDraw",
                column: "TournamentId",
                unique: true,
                filter: "[TournamentId] IS NOT NULL");
        }
    }
}
