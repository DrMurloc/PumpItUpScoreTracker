using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PiuCenterAliasAndMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChartSkillArchive",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SkillName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsHighlighted = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartSkillArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChartSkillMetric",
                schema: "scores",
                columns: table => new
                {
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MetricName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    Grade = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChartSkillMetric", x => new { x.ChartId, x.Source, x.MetricName });
                });

            migrationBuilder.CreateTable(
                name: "ExternalChartAlias",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ExternalKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    LastCheckedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalChartAlias", x => x.Id);
                });

            // Snapshot the hand-maintained skill tags before the piucenter crawler takes
            // ownership of ChartSkill (first successful crawl truncates and regenerates it).
            migrationBuilder.Sql(
                @"INSERT INTO [scores].[ChartSkillArchive] ([Id], [ChartId], [SkillName], [IsHighlighted], [ArchivedAt])
                  SELECT [Id], [ChartId], [SkillName], [IsHighlighted], SYSDATETIMEOFFSET()
                  FROM [scores].[ChartSkill]");

            migrationBuilder.CreateIndex(
                name: "IX_ChartSkillMetric_Source",
                schema: "scores",
                table: "ChartSkillMetric",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChartAlias_Source_ExternalKey",
                schema: "scores",
                table: "ExternalChartAlias",
                columns: new[] { "Source", "ExternalKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalChartAlias_Source_Status",
                schema: "scores",
                table: "ExternalChartAlias",
                columns: new[] { "Source", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChartSkillArchive",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ChartSkillMetric",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ExternalChartAlias",
                schema: "scores");
        }
    }
}
