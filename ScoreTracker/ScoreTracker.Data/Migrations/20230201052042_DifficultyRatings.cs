using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class DifficultyRatings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ChartDifficultyRating",
                schema: "scores",
                columns: table => new
                {
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Difficulty = table.Column<double>(type: "float", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartDifficultyRating", x => x.ChartId);
                });

            migrationBuilder.CreateTable(
                name: "UserChartDifficultyRating",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scale = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChartDifficultyRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserChartDifficultyRating_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChartDifficultyRating_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "scores",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Chart_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                column: "DifficultyRatingChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChartDifficultyRating_ChartId",
                schema: "scores",
                table: "UserChartDifficultyRating",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserChartDifficultyRating_UserId_ChartId",
                schema: "scores",
                table: "UserChartDifficultyRating",
                columns: new[] { "UserId", "ChartId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart",
                column: "DifficultyRatingChartId",
                principalSchema: "scores",
                principalTable: "ChartDifficultyRating",
                principalColumn: "ChartId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chart_ChartDifficultyRating_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropTable(
                name: "ChartDifficultyRating",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserChartDifficultyRating",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_Chart_DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropColumn(
                name: "DifficultyRatingChartId",
                schema: "scores",
                table: "Chart");
        }
    }
}
