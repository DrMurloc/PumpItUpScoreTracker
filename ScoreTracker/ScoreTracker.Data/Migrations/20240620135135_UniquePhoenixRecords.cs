using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniquePhoenixRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "UserId", "ChartId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_UserId_ChartId",
                schema: "scores",
                table: "PhoenixRecord");

            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_UserId",
                schema: "scores",
                table: "PhoenixRecord",
                column: "UserId");
        }
    }
}
