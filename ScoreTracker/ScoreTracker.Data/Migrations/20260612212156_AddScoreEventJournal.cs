using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreEventJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScoreEventJournal",
                schema: "scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MixId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: true),
                    Plate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsBroken = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScoreEventJournal", x => x.Id);
                });

            // Seed the journal with every stored best attempt so history starts from the
            // best-known state rather than empty. RecordedDate is the best attempt's last
            // update, not the original play time — hence the distinct 'backfill' source.
            migrationBuilder.Sql(@"
INSERT INTO [scores].[ScoreEventJournal] (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken)
SELECT NEWID(), NEWID(), RecordedDate, 'backfill', '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B', UserId, ChartId, Score, Plate, IsBroken
FROM [scores].[PhoenixBestAttempt]");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_UserId_ChartId_OccurredAt",
                schema: "scores",
                table: "ScoreEventJournal",
                columns: new[] { "UserId", "ChartId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScoreEventJournal",
                schema: "scores");
        }
    }
}
