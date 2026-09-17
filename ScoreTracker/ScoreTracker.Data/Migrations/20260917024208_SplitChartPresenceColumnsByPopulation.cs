using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitChartPresenceColumnsByPopulation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ChartPumbilityPresenceColumn",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn");

            // A census already stored was counted over everyone, so its columns keep that layout.
            migrationBuilder.AddColumn<bool>(
                name: "CountsBoardPlayers",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChartPumbilityPresenceColumn",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn",
                columns: new[] { "MixId", "CountsBoardPlayers", "ColumnOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One layout fits the old key; the one counted over PIU Scores accounts alone goes.
            migrationBuilder.Sql("DELETE FROM scores.ChartPumbilityPresenceColumn WHERE CountsBoardPlayers = 0");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ChartPumbilityPresenceColumn",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn");

            migrationBuilder.DropColumn(
                name: "CountsBoardPlayers",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ChartPumbilityPresenceColumn",
                schema: "scores",
                table: "ChartPumbilityPresenceColumn",
                columns: new[] { "MixId", "ColumnOrder" });
        }
    }
}
