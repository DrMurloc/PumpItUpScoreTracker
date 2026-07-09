using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreHighlightCaptionDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FolderDebutOrdinal",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeerBetterCount",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeerCount",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeerPgCount",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PumbilityRank",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SkillTitleName",
                schema: "scores",
                table: "ScoreHighlight",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillTitleScore",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkillTitleThreshold",
                schema: "scores",
                table: "ScoreHighlight",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderDebutOrdinal",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PeerBetterCount",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PeerCount",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PeerPgCount",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "PumbilityRank",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "SkillTitleName",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "SkillTitleScore",
                schema: "scores",
                table: "ScoreHighlight");

            migrationBuilder.DropColumn(
                name: "SkillTitleThreshold",
                schema: "scores",
                table: "ScoreHighlight");
        }
    }
}
