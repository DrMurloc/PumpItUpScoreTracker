using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class SupplementedPlacements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfficialWeeklyHighlight_SnapshotId",
                schema: "scores",
                table: "OfficialWeeklyHighlight");

            migrationBuilder.DropIndex(
                name: "IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId",
                schema: "scores",
                table: "OfficialLeaderboardPlacement");

            migrationBuilder.AddColumn<bool>(
                name: "IsSupplemented",
                schema: "scores",
                table: "OfficialWeeklyHighlight",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsSupplemented",
                schema: "scores",
                table: "OfficialLeaderboardPlacement",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWeeklyHighlight_SnapshotId_IsSupplemented",
                schema: "scores",
                table: "OfficialWeeklyHighlight",
                columns: new[] { "SnapshotId", "IsSupplemented" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId",
                schema: "scores",
                table: "OfficialLeaderboardPlacement",
                columns: new[] { "PlayerId", "SnapshotId" })
                .Annotation("SqlServer:Include", new[] { "LeaderboardId", "Place", "Score", "IsSupplemented" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OfficialWeeklyHighlight_SnapshotId_IsSupplemented",
                schema: "scores",
                table: "OfficialWeeklyHighlight");

            migrationBuilder.DropIndex(
                name: "IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId",
                schema: "scores",
                table: "OfficialLeaderboardPlacement");

            migrationBuilder.DropColumn(
                name: "IsSupplemented",
                schema: "scores",
                table: "OfficialWeeklyHighlight");

            migrationBuilder.DropColumn(
                name: "IsSupplemented",
                schema: "scores",
                table: "OfficialLeaderboardPlacement");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWeeklyHighlight_SnapshotId",
                schema: "scores",
                table: "OfficialWeeklyHighlight",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId",
                schema: "scores",
                table: "OfficialLeaderboardPlacement",
                columns: new[] { "PlayerId", "SnapshotId" })
                .Annotation("SqlServer:Include", new[] { "LeaderboardId", "Place", "Score" });
        }
    }
}
