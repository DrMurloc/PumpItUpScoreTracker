using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChartVideoSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Side",
                schema: "scores",
                table: "ChartVideo",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            // Backfill (docs/design/video-sides.md): a URL held by exactly two Single charts of
            // one song is a two-sided video — lower level plays Left. Levels compare on the first
            // modern mix carrying BOTH charts (Phoenix 2 → Phoenix → XX; the GUIDs are the seeded
            // MixIds constants), never across two different mixes, and a tie assigns nothing.
            // Single+Performance pairs stay NULL: their sides are hand-researched, not derived.
            migrationBuilder.Sql(@"
WITH pair AS (
    SELECT va.ChartId AS ChartA, vb.ChartId AS ChartB
    FROM scores.ChartVideo va
    JOIN scores.ChartVideo vb
        ON vb.VideoUrl = va.VideoUrl AND va.ChartId < vb.ChartId
    JOIN scores.Chart ca ON ca.Id = va.ChartId
    JOIN scores.Chart cb ON cb.Id = vb.ChartId
    WHERE ca.SongId = cb.SongId
      AND ca.Type = 'Single' AND cb.Type = 'Single'
      AND (SELECT COUNT(*) FROM scores.ChartVideo vc WHERE vc.VideoUrl = va.VideoUrl) = 2
),
levelled AS (
    SELECT p.ChartA, p.ChartB,
           COALESCE(
               CASE WHEN p2a.Level IS NOT NULL AND p2b.Level IS NOT NULL THEN p2a.Level END,
               CASE WHEN p1a.Level IS NOT NULL AND p1b.Level IS NOT NULL THEN p1a.Level END,
               CASE WHEN xxa.Level IS NOT NULL AND xxb.Level IS NOT NULL THEN xxa.Level END) AS LevelA,
           COALESCE(
               CASE WHEN p2a.Level IS NOT NULL AND p2b.Level IS NOT NULL THEN p2b.Level END,
               CASE WHEN p1a.Level IS NOT NULL AND p1b.Level IS NOT NULL THEN p1b.Level END,
               CASE WHEN xxa.Level IS NOT NULL AND xxb.Level IS NOT NULL THEN xxb.Level END) AS LevelB
    FROM pair p
    LEFT JOIN scores.ChartMix p2a ON p2a.ChartId = p.ChartA AND p2a.MixId = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
    LEFT JOIN scores.ChartMix p2b ON p2b.ChartId = p.ChartB AND p2b.MixId = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
    LEFT JOIN scores.ChartMix p1a ON p1a.ChartId = p.ChartA AND p1a.MixId = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'
    LEFT JOIN scores.ChartMix p1b ON p1b.ChartId = p.ChartB AND p1b.MixId = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B'
    LEFT JOIN scores.ChartMix xxa ON xxa.ChartId = p.ChartA AND xxa.MixId = '20F8CCF8-94B1-418D-B923-C375B042BDA8'
    LEFT JOIN scores.ChartMix xxb ON xxb.ChartId = p.ChartB AND xxb.MixId = '20F8CCF8-94B1-418D-B923-C375B042BDA8'
)
UPDATE v
SET Side = CASE
    WHEN (v.ChartId = l.ChartA AND l.LevelA < l.LevelB) OR (v.ChartId = l.ChartB AND l.LevelB < l.LevelA)
        THEN 'Left'
    ELSE 'Right'
END
FROM scores.ChartVideo v
JOIN levelled l ON v.ChartId IN (l.ChartA, l.ChartB)
WHERE l.LevelA IS NOT NULL AND l.LevelB IS NOT NULL AND l.LevelA <> l.LevelB;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Side",
                schema: "scores",
                table: "ChartVideo");
        }
    }
}
