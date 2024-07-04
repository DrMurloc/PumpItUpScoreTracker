using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TournamentMachines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TournamentMachine",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TournamentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsWarmup = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TournamentMachine", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TournamentMachine_TournamentId",
                schema: "scores",
                table: "TournamentMachine",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TournamentMachine",
                schema: "scores");
        }
    }
}
