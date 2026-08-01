using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Turns the hardcoded PIU Tracker integration into an ordinary registered tool, and moves
    ///     the players who were using it across without interrupting them.
    ///     <para>
    ///         Before this, "Also send scores to piutracker.app" was a checkbox on two pages and a
    ///         boolean threaded through four commands. Every player who ticked it was already having
    ///         their piugame.com session handed to piutracker on every import — this migration does
    ///         not start that, it records it. 653 players had it on and 292 had explicitly turned it
    ///         off when this was written; only the former get a share, and the latter are left alone
    ///         rather than defaulted into one.
    ///     </para>
    ///     <para>
    ///         The tool is Public and approved on arrival because it has been running in production
    ///         for years — sending it through the review queue would take a working integration away
    ///         from those players until an admin clicked a button. It does <b>not</b> accept the
    ///         all-tools pool: session mode is excluded from blanket consent by query, and this row
    ///         says the same thing a second way.
    ///     </para>
    ///     <para>
    ///         Idempotent on both halves, because a migration replayed against a partially-migrated
    ///         database must not duplicate a tool or re-grant a share a player has since revoked.
    ///     </para>
    /// </summary>
    public partial class SeedPiuTrackerTool : Migration
    {
        /// <summary>
        ///     Well-known so the delivery client can recognise it and keep sending PIU Tracker's own
        ///     wire shape. Also in <c>PiuTrackerSessionShape.ToolId</c> — the two must agree, and a
        ///     test says so.
        /// </summary>
        private const string ToolId = "7B1B7F8E-6F1E-4C4B-9F3E-2C0D5A9E4B10";

        /// <summary>TUSA, who runs it.</summary>
        private const string OwnerUserId = "ACCD2EE1-869E-4567-9EB1-65CF8D5CF49A";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
INSERT INTO scores.Tool
    (Id, OwnerUserId, Name, Description, Url, Visibility, AcceptsAllToolsShare,
     WebhookMode, WebhookUrl, CreatedAt, ApprovedAt)
SELECT '{ToolId}', '{OwnerUserId}', 'PIU Tracker',
       'Score tracking and analysis for Pump It Up, run by TUSA. Imports your scores from piugame.com itself.',
       'https://piutracker.app/', 'Public', 0,
       'PiuGameSession', 'https://piutracker.app:3002/api/sync', SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET()
WHERE NOT EXISTS (SELECT 1 FROM scores.Tool t WHERE t.Id = '{ToolId}');");

            // Both places the old opt-in lived: the Upload page's setting and the home-page widget's
            // config. Settings are one JSON blob per user rather than a row per key, so this is a
            // string match on the stored pair — exact, including the casing the writer used.
            migrationBuilder.Sql($@"
INSERT INTO scores.ToolShare (Id, ToolId, UserId, Source, GrantedAt)
SELECT NEWID(), '{ToolId}', o.UserId, 'Direct', SYSDATETIMEOFFSET()
FROM (
    SELECT s.UserId
    FROM scores.UserSettings s
    WHERE s.UiSettings LIKE '%""UploadPhoenixScores__ImportPiuTracker"":""True""%'
    UNION
    SELECT p.UserId
    FROM scores.HomePageWidget w
    INNER JOIN scores.HomePage p ON p.Id = w.PageId
    WHERE w.WidgetType = 'import-scores' AND w.ConfigJson LIKE '%""syncPiuTracker"":true%'
) o
WHERE NOT EXISTS (
    SELECT 1 FROM scores.ToolShare e WHERE e.ToolId = '{ToolId}' AND e.UserId = o.UserId);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
DELETE FROM scores.ToolShare WHERE ToolId = '{ToolId}';
DELETE FROM scores.Tool WHERE Id = '{ToolId}';");
        }
    }
}
