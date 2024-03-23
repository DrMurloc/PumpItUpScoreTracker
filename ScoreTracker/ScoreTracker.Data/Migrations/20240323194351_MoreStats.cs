using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class MoreStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AverageCoOpScore",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageDoublesLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "AverageDoublesScore",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageSinglesLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "AverageSinglesScore",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "AverageSkillLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "AverageSkillScore",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClearCount",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HighestLevel",
                schema: "scores",
                table: "PlayerStats",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageCoOpScore",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageDoublesLevel",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageDoublesScore",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageSinglesLevel",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageSinglesScore",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageSkillLevel",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "AverageSkillScore",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "ClearCount",
                schema: "scores",
                table: "PlayerStats");

            migrationBuilder.DropColumn(
                name: "HighestLevel",
                schema: "scores",
                table: "PlayerStats");
        }
    }
}
