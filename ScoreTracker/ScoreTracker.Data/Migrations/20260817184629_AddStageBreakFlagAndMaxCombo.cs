using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStageBreakFlagAndMaxCombo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStageBroken",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxCombo",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCombo",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStageBroken",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "MaxCombo",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "MaxCombo",
                schema: "scores",
                table: "PhoenixRecord");
        }
    }
}
