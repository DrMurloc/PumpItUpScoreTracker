using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartCommentAnchor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AnchorAt",
                schema: "scores",
                table: "ChartCommentArchive",
                type: "decimal(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnchorAt",
                schema: "scores",
                table: "ChartComment",
                type: "decimal(9,3)",
                precision: 9,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnchorAt",
                schema: "scores",
                table: "ChartCommentArchive");

            migrationBuilder.DropColumn(
                name: "AnchorAt",
                schema: "scores",
                table: "ChartComment");
        }
    }
}
