using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStageBreakPinIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_ChartId_MixId_StageBreaks",
                schema: "scores",
                table: "ScoreEventJournal",
                columns: new[] { "ChartId", "MixId" },
                filter: "[IsStageBroken] = 1")
                .Annotation("SqlServer:Include", new[] { "UserId", "Perfects", "Greats", "Goods", "Bads", "Misses", "IsNonLifebarBreak" })
                .Annotation("SqlServer:Online", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoreEventJournal_ChartId_MixId_StageBreaks",
                schema: "scores",
                table: "ScoreEventJournal");
        }
    }
}
