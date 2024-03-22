using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class PlayerStats : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerStats",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalRating = table.Column<int>(type: "int", nullable: false),
                    SkillRating = table.Column<int>(type: "int", nullable: false),
                    SinglesRating = table.Column<int>(type: "int", nullable: false),
                    DoublesRating = table.Column<int>(type: "int", nullable: false),
                    CoOpRating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerStats", x => x.UserId);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerStats",
                schema: "scores");
        }
    }
}
