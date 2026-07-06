using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerSessionsAndHighlights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "scores",
                table: "PhoenixRecord",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PlayerMilestone",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OldValue = table.Column<double>(type: "float", nullable: true),
                    NewValue = table.Column<double>(type: "float", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerMilestone", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScoreHighlight",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Flags = table.Column<int>(type: "int", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ScoringLevel = table.Column<double>(type: "float", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreHighlight", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_SessionId",
                schema: "scores",
                table: "ScoreEventJournal",
                column: "SessionId",
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_UserId_MixId_OccurredAt",
                schema: "scores",
                table: "ScoreEventJournal",
                columns: new[] { "UserId", "MixId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerMilestone_UserId_MixId_OccurredAt",
                schema: "scores",
                table: "PlayerMilestone",
                columns: new[] { "UserId", "MixId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHighlight_SessionId",
                schema: "scores",
                table: "ScoreHighlight",
                column: "SessionId",
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreHighlight_UserId_MixId_OccurredAt",
                schema: "scores",
                table: "ScoreHighlight",
                columns: new[] { "UserId", "MixId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerMilestone",
                schema: "scores");

            migrationBuilder.DropTable(
                name: "ScoreHighlight",
                schema: "scores");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEventJournal_SessionId",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropIndex(
                name: "IX_ScoreEventJournal_UserId_MixId_OccurredAt",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "Source",
                schema: "scores",
                table: "PhoenixRecord");
        }
    }
}
