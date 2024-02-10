using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class WorldRankingEntity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWorldRanking",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AverageLevel = table.Column<double>(type: "float", nullable: false),
                    AverageScore = table.Column<int>(type: "int", nullable: false),
                    SinglesCount = table.Column<int>(type: "int", nullable: false),
                    DoublesCount = table.Column<int>(type: "int", nullable: false),
                    TotalRating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorldRanking", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWorldRanking_UserName",
                schema: "scores",
                table: "UserWorldRanking",
                column: "UserName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWorldRanking",
                schema: "scores");
        }
    }
}
