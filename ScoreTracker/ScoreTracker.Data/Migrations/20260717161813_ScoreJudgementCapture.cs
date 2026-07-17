using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScoreJudgementCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bads",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goods",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Greats",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Misses",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Perfects",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Bads",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Goods",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Greats",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Misses",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Perfects",
                schema: "scores",
                table: "PhoenixRecord",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bads",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Goods",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Greats",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Misses",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Perfects",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Bads",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "Goods",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "Greats",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "Misses",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.DropColumn(
                name: "Perfects",
                schema: "scores",
                table: "PhoenixRecord");
        }
    }
}
