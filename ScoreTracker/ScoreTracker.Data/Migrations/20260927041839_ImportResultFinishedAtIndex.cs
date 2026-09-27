using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportResultFinishedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportResult_SessionId",
                schema: "scores",
                table: "ImportResult");

            migrationBuilder.CreateIndex(
                name: "IX_ImportResult_FinishedAt",
                schema: "scores",
                table: "ImportResult",
                column: "FinishedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportResult_FinishedAt",
                schema: "scores",
                table: "ImportResult");

            migrationBuilder.CreateIndex(
                name: "IX_ImportResult_SessionId",
                schema: "scores",
                table: "ImportResult",
                column: "SessionId");
        }
    }
}
