using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropChartSimilaritySharedScorers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharedScorers",
                schema: "scores",
                table: "ChartSimilarity");

            // Every banked edge was scored by a formula that no longer exists, and its
            // SignalsJson names signals that are gone. The graph is derived data rebuilt
            // wholesale nightly, so clearing it costs one job run and is the only thing
            // that stops the shelf rendering a score nothing can explain.
            migrationBuilder.Sql("DELETE FROM scores.ChartSimilarity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SharedScorers",
                schema: "scores",
                table: "ChartSimilarity",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
