using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHardmodeHeldPerPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HardmodeDoublesChartsHeld",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HardmodeSinglesChartsHeld",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DoublesChartsHeld",
                schema: "scores",
                table: "OfficialHardmodeRating",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SinglesChartsHeld",
                schema: "scores",
                table: "OfficialHardmodeRating",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HardmodeDoublesChartsHeld",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "HardmodeSinglesChartsHeld",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "DoublesChartsHeld",
                schema: "scores",
                table: "OfficialHardmodeRating");

            migrationBuilder.DropColumn(
                name: "SinglesChartsHeld",
                schema: "scores",
                table: "OfficialHardmodeRating");
        }
    }
}
