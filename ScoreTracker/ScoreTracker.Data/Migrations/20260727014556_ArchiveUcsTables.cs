using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArchiveUcsTables : Migration
    {
        /// <summary>
        ///     The UCS vertical is gone, but its rows are real user submissions and the owner
        ///     may revive the feature. Scaffolding produced DropTable for all three; they are
        ///     renamed instead, so the model no longer knows about them while the data stays
        ///     on disk. Dropping them for real is a later, deliberate call.
        ///     sp_rename does not rename a table's PK or indexes, so those keep their original
        ///     names (PK_UcsChart, IX_UcsChart_PiuGameId, ...). That only matters if a future
        ///     UcsChart is ever created alongside these — it would collide on constraint names.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UcsChart",
                schema: "scores",
                newName: "UcsChart_archived",
                newSchema: "scores");

            migrationBuilder.RenameTable(
                name: "UcsChartLeaderboardEntry",
                schema: "scores",
                newName: "UcsChartLeaderboardEntry_archived",
                newSchema: "scores");

            migrationBuilder.RenameTable(
                name: "UcsChartTag",
                schema: "scores",
                newName: "UcsChartTag_archived",
                newSchema: "scores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UcsChart_archived",
                schema: "scores",
                newName: "UcsChart",
                newSchema: "scores");

            migrationBuilder.RenameTable(
                name: "UcsChartLeaderboardEntry_archived",
                schema: "scores",
                newName: "UcsChartLeaderboardEntry",
                newSchema: "scores");

            migrationBuilder.RenameTable(
                name: "UcsChartTag_archived",
                schema: "scores",
                newName: "UcsChartTag",
                newSchema: "scores");
        }
    }
}
