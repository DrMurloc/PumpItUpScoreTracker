using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class FolderBaselinePresenceBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "QualifiedCount",
                schema: "scores",
                table: "ChartFolderBaseline",
                newName: "PresentCount");

            migrationBuilder.AddColumn<decimal>(
                name: "PresenceCutoff",
                schema: "scores",
                table: "ChartFolderBaseline",
                type: "decimal(9,4)",
                precision: 9,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresenceCutoff",
                schema: "scores",
                table: "ChartFolderBaseline");

            migrationBuilder.RenameColumn(
                name: "PresentCount",
                schema: "scores",
                table: "ChartFolderBaseline",
                newName: "QualifiedCount");
        }
    }
}
