using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerHistory",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompetitiveLevel = table.Column<double>(type: "float", nullable: false),
                    SinglesLevel = table.Column<double>(type: "float", nullable: false),
                    DoublesLevel = table.Column<double>(type: "float", nullable: false),
                    CoOpRating = table.Column<int>(type: "int", nullable: false),
                    PassCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerHistory_UserId",
                schema: "scores",
                table: "PlayerHistory",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerHistory",
                schema: "scores");
        }
    }
}
