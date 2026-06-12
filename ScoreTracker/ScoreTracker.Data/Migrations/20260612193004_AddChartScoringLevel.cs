using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartScoringLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartScoringLevel",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoringLevel = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartScoringLevel", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChartScoringLevel_ChartId_MixId",
                schema: "scores",
                table: "ChartScoringLevel",
                columns: new[] { "ChartId", "MixId" },
                unique: true);

            // Backfill from the Catalog-owned ChartMix rows (rearch F4); the old column
            // is dropped by a follow-up migration once nothing reads it.
            migrationBuilder.Sql(@"
INSERT INTO [scores].[ChartScoringLevel] ([Id], [ChartId], [MixId], [ScoringLevel])
SELECT NEWID(), [ChartId], [MixId], [ScoringLevel]
FROM [scores].[ChartMix]
WHERE [ScoringLevel] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartScoringLevel",
                schema: "scores");
        }
    }
}
