using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    public partial class AddChartLists : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedChart",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ListName = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedChart", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedChart_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedChart_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "scores",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedChart_ChartId",
                schema: "scores",
                table: "SavedChart",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedChart_ListName",
                schema: "scores",
                table: "SavedChart",
                column: "ListName");

            migrationBuilder.CreateIndex(
                name: "IX_SavedChart_UserId",
                schema: "scores",
                table: "SavedChart",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedChart",
                schema: "scores");
        }
    }
}
