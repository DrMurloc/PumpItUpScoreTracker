using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStageBreakCause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNonLifebarBreak",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PassGrade",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PassPlate",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsNonLifebarBreak",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "PassGrade",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "PassPlate",
                schema: "scores",
                table: "ScoreEventJournal");
        }
    }
}
