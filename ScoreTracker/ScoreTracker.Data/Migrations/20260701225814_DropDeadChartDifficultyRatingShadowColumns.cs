using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropDeadChartDifficultyRatingShadowColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropIndex(
                name: "IX_Chart_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropColumn(
                name: "DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropColumn(
                name: "DifficultyRatingMixId",
                schema: "scores",
                table: "Chart");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Chart_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                columns: new[] { "DifficultyRatingChartId", "DifficultyRatingMixId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId_DifficultyRatingMixId",
                schema: "scores",
                table: "Chart",
                columns: new[] { "DifficultyRatingChartId", "DifficultyRatingMixId" },
                principalSchema: "scores",
                principalTable: "ChartDifficultyRating",
                principalColumns: new[] { "ChartId", "MixId" });
        }
    }
}
