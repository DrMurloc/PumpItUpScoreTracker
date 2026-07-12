using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class TierListUserMaterializationAndChartStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartScoreStats",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreStandardDeviation = table.Column<double>(type: "float", nullable: false),
                    ScoreCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartScoreStats", x => new { x.MixId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_ChartScoreStats_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTierListEntry",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTierListEntry", x => new { x.MixId, x.UserId, x.ChartId });
                    table.ForeignKey(
                        name: "FK_UserTierListEntry_Chart_ChartId",
                        column: x => x.ChartId,
                        principalSchema: "scores",
                        principalTable: "Chart",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTierListEntry_User_UserId",
                        column: x => x.UserId,
                        principalSchema: "scores",
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartScoreStats_ChartId",
                schema: "scores",
                table: "ChartScoreStats",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTierListEntry_ChartId",
                schema: "scores",
                table: "UserTierListEntry",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTierListEntry_MixId_ChartId",
                schema: "scores",
                table: "UserTierListEntry",
                columns: new[] { "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "Category", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTierListEntry_UserId",
                schema: "scores",
                table: "UserTierListEntry",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartScoreStats",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "UserTierListEntry",
                schema: "scores");
        }
    }
}
