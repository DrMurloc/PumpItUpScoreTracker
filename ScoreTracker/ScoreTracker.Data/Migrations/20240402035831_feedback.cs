using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class feedback : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuggestionFeedback",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestionCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPositive = table.Column<bool>(type: "bit", nullable: false),
                    ShouldHide = table.Column<bool>(type: "bit", nullable: false),
                    FeedbackCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuggestionFeedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionFeedback_ChartId",
                schema: "scores",
                table: "SuggestionFeedback",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_SuggestionFeedback_UserId",
                schema: "scores",
                table: "SuggestionFeedback",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuggestionFeedback",
                schema: "scores");
        }
    }
}
