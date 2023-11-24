using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class OfficialLeaderboard : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserOfficialLeaderboard",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LeaderboardType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LeaderboardName = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserOfficialLeaderboard", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserOfficialLeaderboard_Username",
                schema: "scores",
                table: "UserOfficialLeaderboard",
                column: "Username");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserOfficialLeaderboard",
                schema: "scores");
        }
    }
}
