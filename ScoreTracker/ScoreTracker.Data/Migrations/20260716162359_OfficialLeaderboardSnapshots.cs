using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class OfficialLeaderboardSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfficialBoardRecord",
                schema: "scores",
                columns: table => new
                {
                    LeaderboardId = table.Column<int>(type: "int", nullable: false),
                    HighScore = table.Column<int>(type: "int", nullable: false),
                    AchievedSnapshotId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialBoardRecord", x => x.LeaderboardId);
                });

            migrationBuilder.CreateTable(
                name: "OfficialChartPopularity",
                schema: "scores",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(type: "int", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialChartPopularity", x => new { x.SnapshotId, x.ChartId });
                });

            migrationBuilder.CreateTable(
                name: "OfficialFolderRecord",
                schema: "scores",
                columns: table => new
                {
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    HighScore = table.Column<int>(type: "int", nullable: false),
                    AchievedSnapshotId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialFolderRecord", x => new { x.MixId, x.ChartType, x.Level });
                });

            migrationBuilder.CreateTable(
                name: "OfficialLeaderboard",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaderboardType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChartType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Level = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialLeaderboard", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialLeaderboardPlacement",
                schema: "scores",
                columns: table => new
                {
                    SnapshotId = table.Column<int>(type: "int", nullable: false),
                    LeaderboardId = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    Place = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialLeaderboardPlacement", x => new { x.SnapshotId, x.LeaderboardId, x.Place, x.PlayerId });
                });

            migrationBuilder.CreateTable(
                name: "OfficialLeaderboardSnapshot",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsBaseline = table.Column<bool>(type: "bit", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    BoardsExpected = table.Column<int>(type: "int", nullable: false),
                    BoardsWritten = table.Column<int>(type: "int", nullable: false),
                    BoardsSkipped = table.Column<int>(type: "int", nullable: false),
                    Error = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialLeaderboardSnapshot", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialPlayer",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AvatarUrl = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserIdSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialPlayer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialPlayerRenameProposal",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OldPlayerId = table.Column<int>(type: "int", nullable: false),
                    NewPlayerId = table.Column<int>(type: "int", nullable: false),
                    OldUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NewUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AvatarMatched = table.Column<bool>(type: "bit", nullable: false),
                    Top50Overlap = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedSnapshotId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialPlayerRenameProposal", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OfficialWeeklyHighlight",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SnapshotId = table.Column<int>(type: "int", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    PlayerId = table.Column<int>(type: "int", nullable: false),
                    DethronedPlayerId = table.Column<int>(type: "int", nullable: true),
                    LeaderboardId = table.Column<int>(type: "int", nullable: true),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChartType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Level = table.Column<int>(type: "int", nullable: true),
                    GradeBand = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Score = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    PrevValue = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    NewValue = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfficialWeeklyHighlight", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboard_ChartId",
                schema: "scores",
                table: "OfficialLeaderboard",
                column: "ChartId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboard_MixId_LeaderboardType_Name",
                schema: "scores",
                table: "OfficialLeaderboard",
                columns: new[] { "MixId", "LeaderboardType", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboardPlacement_PlayerId_SnapshotId",
                schema: "scores",
                table: "OfficialLeaderboardPlacement",
                columns: new[] { "PlayerId", "SnapshotId" })
                .Annotation("SqlServer:Include", new[] { "LeaderboardId", "Place", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialLeaderboardSnapshot_MixId_CompletedAt",
                schema: "scores",
                table: "OfficialLeaderboardSnapshot",
                columns: new[] { "MixId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialPlayer_MixId_Username",
                schema: "scores",
                table: "OfficialPlayer",
                columns: new[] { "MixId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfficialPlayer_UserId",
                schema: "scores",
                table: "OfficialPlayer",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_OfficialPlayerRenameProposal_MixId_Status",
                schema: "scores",
                table: "OfficialPlayerRenameProposal",
                columns: new[] { "MixId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OfficialWeeklyHighlight_SnapshotId",
                schema: "scores",
                table: "OfficialWeeklyHighlight",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfficialBoardRecord",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialChartPopularity",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialFolderRecord",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialLeaderboard",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialLeaderboardPlacement",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialLeaderboardSnapshot",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialPlayer",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialPlayerRenameProposal",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "OfficialWeeklyHighlight",
                schema: "scores");
        }
    }
}
