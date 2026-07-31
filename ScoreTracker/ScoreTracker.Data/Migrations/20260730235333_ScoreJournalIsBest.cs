using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScoreJournalIsBest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "WeeklyUserEntry",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "UserWeeklyPlacing",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "UserDailyStepPlacing",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            // Defaults TRUE, which also backfills every existing row: the journal was
            // progress-only, so nothing reached it that did not change the best attempt. That
            // includes the plate-leak regressions — those DID become the record, which is
            // exactly the bug, and marking them false would be revisionist. The default stays
            // true afterwards because only the observation path writes false, and it always
            // says so explicitly.
            migrationBuilder.AddColumn<bool>(
                name: "IsBest",
                schema: "scores",
                table: "ScoreEventJournal",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "DailyStepEntry",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            // A journal row is one play, keyed by the site's play time, and the unique index
            // below is what keeps a re-imported recently-played window from duplicating it.
            // A handful of historical rows share a key (measured 2026-07-30: 14 rows across 7
            // keys out of 1,039,857); keep the best one per key so the survivor is the row a
            // chart's history should show.
            migrationBuilder.Sql(@"
WITH ranked AS (
    SELECT Id, ROW_NUMBER() OVER (
        PARTITION BY UserId, MixId, ChartId, OccurredAt
        ORDER BY IsBroken, ISNULL(Score, -1) DESC, Id) AS rn
    FROM [scores].[ScoreEventJournal])
DELETE FROM ranked WHERE rn > 1;");

            migrationBuilder.CreateIndex(
                name: "IX_ScoreEventJournal_UserId_MixId_ChartId_OccurredAt",
                schema: "scores",
                table: "ScoreEventJournal",
                columns: new[] { "UserId", "MixId", "ChartId", "OccurredAt" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoreEventJournal_UserId_MixId_ChartId_OccurredAt",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.DropColumn(
                name: "IsBest",
                schema: "scores",
                table: "ScoreEventJournal");

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "WeeklyUserEntry",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "UserWeeklyPlacing",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "UserDailyStepPlacing",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Plate",
                schema: "scores",
                table: "DailyStepEntry",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
