using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerFolderLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerFolderLevel",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    Played = table.Column<int>(type: "int", nullable: false),
                    AverageScore = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerFolderLevel", x => new { x.UserId, x.MixId, x.ChartType, x.Level });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerFolderLevel",
                schema: "scores");
        }
    }
}
