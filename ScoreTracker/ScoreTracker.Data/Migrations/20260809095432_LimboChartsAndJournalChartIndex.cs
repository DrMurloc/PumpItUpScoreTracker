using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class LimboChartsAndJournalChartIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LimboChart",
                schema: "scores",
                columns: table => new
                {
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LimboChart", x => new { x.MixId, x.ChartId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_ChartId_MixId",
                schema: "scores",
                table: "ScoreEventJournal",
                columns: new[] { "ChartId", "MixId" })
                .Annotation("SqlServer:Include", new[] { "UserId", "Score", "IsBroken", "OccurredAt" })
                .Annotation("SqlServer:Online", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LimboChart",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEventJournal_ChartId_MixId",
                schema: "scores",
                table: "ScoreEventJournal");
        }
    }
}
