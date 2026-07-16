using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartSimilarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartSimilarity",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SimilarChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<double>(type: "float", nullable: false),
                    SignalsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SharedScorers = table.Column<int>(type: "int", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartSimilarity", x => new { x.MixId, x.ChartId, x.SimilarChartId });
                    table.ForeignKey(
                        name: "FK_ChartSimilarity_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChartSimilarity_Chart_SimilarChartId",
                        column: x => x.SimilarChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartSimilarity_ChartId",
                schema: "scores",
                table: "ChartSimilarity",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_ChartSimilarity_SimilarChartId",
                schema: "scores",
                table: "ChartSimilarity",
                column: "SimilarChartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartSimilarity",
                schema: "scores");
        }
    }
}
