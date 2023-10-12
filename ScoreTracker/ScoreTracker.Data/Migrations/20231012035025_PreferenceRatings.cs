using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class PreferenceRatings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartPreferenceRating",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartPreferenceRating", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferenceRating",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferenceRating", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartPreferenceRating_MixId_ChartId",
                schema: "scores",
                table: "ChartPreferenceRating",
                columns: new[] { "MixId", "ChartId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPreferenceRating_MixId_UserId_ChartId",
                schema: "scores",
                table: "UserPreferenceRating",
                columns: new[] { "MixId", "UserId", "ChartId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartPreferenceRating",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserPreferenceRating",
                schema: "scores");
        }
    }
}
