using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class LegacyChartSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BestAttempt_UserId_ChartId",
                schema: "scores",
                table: "BestAttempt");

            migrationBuilder.AddColumn<string>(
                name: "LegacySlot",
                schema: "scores",
                table: "ChartMix",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PlayerCount",
                schema: "scores",
                table: "Chart",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Retire the level-as-player-count pun for the rows that used it: mainline
            // co-ops store their player count in Level (they have no difficulty).
            // Self-contained backfill — no external data (docs/design/legacy-mixes.md).
            migrationBuilder.Sql("UPDATE [scores].[Chart] SET [PlayerCount] = [Level] WHERE [Type] = 'CoOp';");

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "BestAttempt",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("20f8ccf8-94b1-418d-b923-c375b042bda8"));

            migrationBuilder.CreateIndex(
                name: "IX_BestAttempt_UserId_ChartId_MixId",
                schema: "scores",
                table: "BestAttempt",
                columns: new[] { "UserId", "ChartId", "MixId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BestAttempt_UserId_ChartId_MixId",
                schema: "scores",
                table: "BestAttempt");

            migrationBuilder.DropColumn(
                name: "LegacySlot",
                schema: "scores",
                table: "ChartMix");

            migrationBuilder.DropColumn(
                name: "PlayerCount",
                schema: "scores",
                table: "Chart");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "BestAttempt");

            migrationBuilder.CreateIndex(
                name: "IX_BestAttempt_UserId_ChartId",
                schema: "scores",
                table: "BestAttempt",
                columns: new[] { "UserId", "ChartId" });
        }
    }
}
