using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeeklyEventMirrorPerMix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeeklyUserEntry_UserId_ChartId",
                schema: "scores",
                table: "WeeklyUserEntry");

            migrationBuilder.DropIndex(
                name: "IX_UserWeeklyPlacing_UserId",
                schema: "scores",
                table: "UserWeeklyPlacing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PastTourneyCharts",
                schema: "scores",
                table: "PastTourneyCharts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OfficialLeaderboardImportState",
                schema: "scores",
                table: "OfficialLeaderboardImportState");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "scores",
                table: "OfficialLeaderboardImportState");

            // Backfill = the Phoenix mix row id (MixIds.Phoenix) on every table below;
            // the ImportState singleton row becomes the Phoenix row, timestamp preserved.
            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "WeeklyUserEntry",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "WeeklyTournamentChart",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserWorldRanking",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserWeeklyPlacing",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserTournamentSession",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "UserOfficialLeaderboard",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "QualifiersConfiguration",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "PastTourneyCharts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddColumn<Guid>(
                name: "MixId",
                schema: "scores",
                table: "OfficialLeaderboardImportState",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("1abb8f5a-bda3-40f0-9ce7-1c4f9f8f1d3b"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_PastTourneyCharts",
                schema: "scores",
                table: "PastTourneyCharts",
                columns: new[] { "ChartId", "MixId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_OfficialLeaderboardImportState",
                schema: "scores",
                table: "OfficialLeaderboardImportState",
                column: "MixId");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyUserEntry_UserId_ChartId_MixId",
                schema: "scores",
                table: "WeeklyUserEntry",
                columns: new[] { "UserId", "ChartId", "MixId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWeeklyPlacing_UserId_MixId",
                schema: "scores",
                table: "UserWeeklyPlacing",
                columns: new[] { "UserId", "MixId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WeeklyUserEntry_UserId_ChartId_MixId",
                schema: "scores",
                table: "WeeklyUserEntry");

            migrationBuilder.DropIndex(
                name: "IX_UserWeeklyPlacing_UserId_MixId",
                schema: "scores",
                table: "UserWeeklyPlacing");

            migrationBuilder.DropPrimaryKey(
                name: "PK_PastTourneyCharts",
                schema: "scores",
                table: "PastTourneyCharts");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OfficialLeaderboardImportState",
                schema: "scores",
                table: "OfficialLeaderboardImportState");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "WeeklyUserEntry");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "WeeklyTournamentChart");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserWorldRanking");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserWeeklyPlacing");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserTournamentSession");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "UserOfficialLeaderboard");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "QualifiersConfiguration");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "PastTourneyCharts");

            migrationBuilder.DropColumn(
                name: "MixId",
                schema: "scores",
                table: "OfficialLeaderboardImportState");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                schema: "scores",
                table: "OfficialLeaderboardImportState",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_PastTourneyCharts",
                schema: "scores",
                table: "PastTourneyCharts",
                column: "ChartId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OfficialLeaderboardImportState",
                schema: "scores",
                table: "OfficialLeaderboardImportState",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyUserEntry_UserId_ChartId",
                schema: "scores",
                table: "WeeklyUserEntry",
                columns: new[] { "UserId", "ChartId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWeeklyPlacing_UserId",
                schema: "scores",
                table: "UserWeeklyPlacing",
                column: "UserId");
        }
    }
}
