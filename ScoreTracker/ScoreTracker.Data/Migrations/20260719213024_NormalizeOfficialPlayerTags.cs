using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Two data repairs, no schema change. First: official-site tags normalize to their
    ///     whitespace-free form — the board scrape yields "TAG#1234" while the account page
    ///     yields "TAG #1234", which split one human into two OfficialPlayer rows; twins merge
    ///     (placements, highlights, proposals re-point; the account link and avatar survive)
    ///     and every remaining tag is squeezed. Second: PhoenixRecord's denormalized
    ///     LetterGrade string recomputes for Phoenix 2 rows below 950k, which were written
    ///     with the Phoenix grade table before score→grade resolution went per-mix.
    /// </summary>
    public partial class NormalizeOfficialPlayerTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE #TagTwins (WinnerId INT NOT NULL, LoserId INT NOT NULL);

WITH Normalized AS (
    SELECT p.Id, p.MixId,
           REPLACE(REPLACE(REPLACE(p.Username, N' ', N''), NCHAR(9), N''), NCHAR(160), N'') AS Tag,
           (SELECT COUNT(*) FROM scores.OfficialLeaderboardPlacement pl WHERE pl.PlayerId = p.Id) AS Placements
    FROM scores.OfficialPlayer p
),
Ranked AS (
    SELECT Id, MixId, Tag,
           ROW_NUMBER() OVER (PARTITION BY MixId, Tag ORDER BY Placements DESC, Id) AS rn
    FROM Normalized
)
INSERT INTO #TagTwins (WinnerId, LoserId)
SELECT w.Id, l.Id
FROM Ranked w
JOIN Ranked l ON l.MixId = w.MixId AND l.Tag = w.Tag AND l.rn > 1
WHERE w.rn = 1;

-- Transition-week collisions: both twins on the same board in the same snapshot — the
-- winner's row is the truth, so the loser's drops instead of colliding on re-point.
DELETE lp
FROM scores.OfficialLeaderboardPlacement lp
JOIN #TagTwins t ON lp.PlayerId = t.LoserId
WHERE EXISTS (SELECT 1 FROM scores.OfficialLeaderboardPlacement w
              WHERE w.PlayerId = t.WinnerId AND w.SnapshotId = lp.SnapshotId
                AND w.LeaderboardId = lp.LeaderboardId);

UPDATE lp SET PlayerId = t.WinnerId
FROM scores.OfficialLeaderboardPlacement lp JOIN #TagTwins t ON lp.PlayerId = t.LoserId;

UPDATE h SET PlayerId = t.WinnerId
FROM scores.OfficialWeeklyHighlight h JOIN #TagTwins t ON h.PlayerId = t.LoserId;
UPDATE h SET DethronedPlayerId = t.WinnerId
FROM scores.OfficialWeeklyHighlight h JOIN #TagTwins t ON h.DethronedPlayerId = t.LoserId;

UPDATE p SET OldPlayerId = t.WinnerId
FROM scores.OfficialPlayerRenameProposal p JOIN #TagTwins t ON p.OldPlayerId = t.LoserId;
UPDATE p SET NewPlayerId = t.WinnerId
FROM scores.OfficialPlayerRenameProposal p JOIN #TagTwins t ON p.NewPlayerId = t.LoserId;
DELETE FROM scores.OfficialPlayerRenameProposal WHERE OldPlayerId = NewPlayerId;

-- The winner keeps any account link or avatar a merged twin carried; last seen = the later.
UPDATE w SET
    UserId = COALESCE(w.UserId, l.UserId),
    UserIdSource = CASE WHEN w.UserId IS NULL AND l.UserId IS NOT NULL
                        THEN l.UserIdSource ELSE w.UserIdSource END,
    AvatarUrl = COALESCE(w.AvatarUrl, l.AvatarUrl),
    LastSeenAt = CASE WHEN l.LastSeenAt > w.LastSeenAt THEN l.LastSeenAt ELSE w.LastSeenAt END
FROM scores.OfficialPlayer w
JOIN #TagTwins t ON w.Id = t.WinnerId
JOIN scores.OfficialPlayer l ON l.Id = t.LoserId;

DELETE p FROM scores.OfficialPlayer p JOIN #TagTwins t ON p.Id = t.LoserId;

UPDATE scores.OfficialPlayer
SET Username = REPLACE(REPLACE(REPLACE(Username, N' ', N''), NCHAR(9), N''), NCHAR(160), N'')
WHERE Username <> REPLACE(REPLACE(REPLACE(Username, N' ', N''), NCHAR(9), N''), NCHAR(160), N'');

DROP TABLE #TagTwins;
");

            migrationBuilder.Sql(@"
-- Phoenix 2 rows below AAA re-grade on the Phoenix 2 floor table (AAA and up is identical
-- across mixes, so those strings already stand).
UPDATE scores.PhoenixRecord
SET LetterGrade = CASE
    WHEN Score >= 940000 THEN N'AA+'
    WHEN Score >= 920000 THEN N'AA'
    WHEN Score >= 900000 THEN N'A+'
    WHEN Score >= 800000 THEN N'A'
    WHEN Score >= 700000 THEN N'B'
    WHEN Score >= 600000 THEN N'C'
    WHEN Score >= 500000 THEN N'D'
    ELSE N'F'
END
WHERE MixId = 'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
  AND Score IS NOT NULL AND Score < 950000;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair only — the pre-merge duplicate rows and the mis-graded letter
            // strings are not worth reconstructing.
        }
    }
}
