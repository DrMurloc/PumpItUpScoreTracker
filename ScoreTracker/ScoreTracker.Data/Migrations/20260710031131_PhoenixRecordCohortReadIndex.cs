using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PhoenixRecordCohortReadIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PhoenixRecord_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord",
                columns: new[] { "MixId", "ChartId" })
                .Annotation("SqlServer:Include", new[] { "UserId", "Score", "Plate", "IsBroken" })
                .Annotation("SqlServer:Online", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhoenixRecord_MixId_ChartId",
                schema: "scores",
                table: "PhoenixRecord");
        }
    }
}
