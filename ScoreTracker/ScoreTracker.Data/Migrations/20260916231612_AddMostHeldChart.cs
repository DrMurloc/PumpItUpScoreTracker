using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMostHeldChart : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MostHeldChart",
                schema: "scores",
                columns: table => new
                {
                    SeasonId = table.Column<short>(type: "smallint", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<double>(type: "float", nullable: false),
                    Holders = table.Column<int>(type: "int", nullable: false),
                    FolderSize = table.Column<int>(type: "int", nullable: false),
                    FolderCut = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MostHeldChart", x => new { x.SeasonId, x.MixId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_MostHeldChart_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MostHeldChart_ChartId",
                schema: "scores",
                table: "MostHeldChart",
                column: "ChartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MostHeldChart",
                schema: "scores");
        }
    }
}
