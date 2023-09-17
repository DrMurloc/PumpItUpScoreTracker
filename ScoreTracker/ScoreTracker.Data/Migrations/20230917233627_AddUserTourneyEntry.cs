using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class AddUserTourneyEntry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTournamentSession",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionScore = table.Column<int>(type: "int", nullable: false),
                    ChartEntries = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTournamentSession", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTournamentSession_TournamentId_UserId",
                schema: "scores",
                table: "UserTournamentSession",
                columns: new[] { "TournamentId", "UserId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTournamentSession",
                schema: "scores");
        }
    }
}
