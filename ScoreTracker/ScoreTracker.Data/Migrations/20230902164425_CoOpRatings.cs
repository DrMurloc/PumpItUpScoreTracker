using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class CoOpRatings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoOpRating",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Player = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoOpRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CoOpRating_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserCoOpRating",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Player = table.Column<int>(type: "int", nullable: false),
                    Difficulty = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserCoOpRating", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserCoOpRating_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserCoOpRating_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "scores",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoOpRating_ChartId",
                schema: "scores",
                table: "CoOpRating",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCoOpRating_ChartId",
                schema: "scores",
                table: "UserCoOpRating",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserCoOpRating_UserId",
                schema: "scores",
                table: "UserCoOpRating",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoOpRating",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserCoOpRating",
                schema: "scores");
        }
    }
}
