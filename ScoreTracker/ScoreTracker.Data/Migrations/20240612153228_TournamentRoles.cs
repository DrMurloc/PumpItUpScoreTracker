using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TournamentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentPlayer",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Seed = table.Column<int>(type: "int", nullable: false),
                    DiscordId = table.Column<decimal>(type: "decimal(20,0)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PotentialConflict = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentPlayer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TournamentRole",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentRole", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentPlayer_TournamentId",
                schema: "scores",
                table: "TournamentPlayer",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_TournamentRole_TournamentId",
                schema: "scores",
                table: "TournamentRole",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentPlayer",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "TournamentRole",
                schema: "scores");
        }
    }
}
