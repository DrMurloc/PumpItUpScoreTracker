using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class HighestTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserHighestTitle",
                schema: "scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TitleName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserHighestTitle", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserHighestTitle_Level",
                schema: "scores",
                table: "UserHighestTitle",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_UserHighestTitle_TitleName",
                schema: "scores",
                table: "UserHighestTitle",
                column: "TitleName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserHighestTitle",
                schema: "scores");
        }
    }
}
