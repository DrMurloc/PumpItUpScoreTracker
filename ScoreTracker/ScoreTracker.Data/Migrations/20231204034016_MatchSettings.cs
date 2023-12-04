using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class MatchSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Match",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Match", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchLink",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromMatch = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ToMatch = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PlayerCount = table.Column<int>(type: "int", nullable: false),
                    IsWinners = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchLink", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RandomSettings",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Json = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RandomSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Match_Name",
                schema: "scores",
                table: "Match",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLink_FromMatch",
                schema: "scores",
                table: "MatchLink",
                column: "FromMatch");

            migrationBuilder.CreateIndex(
                name: "IX_MatchLink_ToMatch",
                schema: "scores",
                table: "MatchLink",
                column: "ToMatch");

            migrationBuilder.CreateIndex(
                name: "IX_RandomSettings_Name",
                schema: "scores",
                table: "RandomSettings",
                column: "Name");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Match",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "MatchLink",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "RandomSettings",
                schema: "scores");
        }
    }
}
