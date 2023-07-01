using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class DifficultyRatingMix : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "ChartDifficultyRating",
                type: "uniqueidentifier",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserChartDifficultyRating");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "ChartDifficultyRating");
        }
    }
}
