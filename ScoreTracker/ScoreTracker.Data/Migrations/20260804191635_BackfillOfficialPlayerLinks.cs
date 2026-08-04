using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Links mirror players to the accounts that already proved them, for accounts whose
    ///     last import predates the link column.
    ///     <para>
    ///         OfficialPlayer.UserId has only existed since 2026-07-17, so it records three
    ///         weeks of importers rather than the import population — 414 accounts have an
    ///         official import somewhere in their history. The recoverable evidence is
    ///         User.GameTag, which is written in exactly two places, both of them piugame
    ///         account data: the import saga and the PIUGAME login. No UI can set it. Matching
    ///         it to a board tag therefore joins two piugame-derived strings rather than
    ///         guessing, which is why this supersedes the overhaul's "no string-matching
    ///         backfill" rule — that rule assumed the column was user-supplied.
    ///     </para>
    ///     <para>
    ///         Import-observed links are never overwritten; this only fills nulls. Where a tag
    ///         is claimed by more than one public account the most recently active in that mix
    ///         wins, which is the rule the live link path already applies.
    ///     </para>
    /// </summary>
    public partial class BackfillOfficialPlayerLinks : Migration
    {
        // Tags collapse to their whitespace-free form at every seam (OfficialPlayerTag): the
        // site renders the same human as "TAG#1234" on a board and "TAG #1234" on the account
        // page. CHAR(160) is the &nbsp; scraped HTML carries.
        private const string NormalizedUsername =
            "REPLACE(REPLACE(REPLACE(REPLACE(UPPER(op.Username), CHAR(160), ''), CHAR(9), ''), CHAR(10), ''), ' ', '')";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($@"
WITH Tagged AS (
    SELECT  u.Id AS UserId,
            m.Id AS MixId,
            REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(u.GameTag)),
                CHAR(160), ''), CHAR(9), ''), CHAR(10), ''), ' ', '') AS Tag,
            (SELECT MAX(r.RecordedDate) FROM scores.PhoenixRecord r
              WHERE r.UserId = u.Id AND r.MixId = m.Id) AS LastActive
    FROM    scores.[User] u
    CROSS JOIN scores.Mix m
    WHERE   u.IsPublic = 1
      AND   u.GameTag IS NOT NULL
      AND   LTRIM(RTRIM(u.GameTag)) <> ''
      AND   m.Name IN ('Phoenix', 'Phoenix2')
),
-- A tag on two accounts is one person's old account and their new one, or two people who
-- typed the same thing into piugame years apart. Either way the scores showing under it
-- belong to whoever is playing under it now.
Winner AS (
    SELECT UserId, MixId, Tag,
           ROW_NUMBER() OVER (PARTITION BY MixId, UPPER(Tag)
                              ORDER BY LastActive DESC, UserId) AS Seat
    FROM   Tagged
    WHERE  LastActive IS NOT NULL
)
UPDATE  op
SET     op.UserId = w.UserId,
        op.UserIdSource = 'GameTag'
FROM    scores.OfficialPlayer op
JOIN    Winner w
  ON    w.MixId = op.MixId
 AND    UPPER(w.Tag) = {NormalizedUsername}
WHERE   w.Seat = 1
  AND   op.UserId IS NULL;
");

            // Accounts the crawl has never seen on a board still belong in the dimension: the
            // supplemented reading is precisely where a player who has never placed becomes
            // visible. Avatar and LastSeenAt fill in on their next sweep or import.
            migrationBuilder.Sql($@"
WITH Tagged AS (
    SELECT  u.Id AS UserId,
            m.Id AS MixId,
            REPLACE(REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(u.GameTag)),
                CHAR(160), ''), CHAR(9), ''), CHAR(10), ''), ' ', '') AS Tag,
            (SELECT MAX(r.RecordedDate) FROM scores.PhoenixRecord r
              WHERE r.UserId = u.Id AND r.MixId = m.Id) AS LastActive
    FROM    scores.[User] u
    CROSS JOIN scores.Mix m
    WHERE   u.IsPublic = 1
      AND   u.GameTag IS NOT NULL
      AND   LTRIM(RTRIM(u.GameTag)) <> ''
      AND   m.Name IN ('Phoenix', 'Phoenix2')
),
Winner AS (
    SELECT UserId, MixId, Tag,
           ROW_NUMBER() OVER (PARTITION BY MixId, UPPER(Tag)
                              ORDER BY LastActive DESC, UserId) AS Seat
    FROM   Tagged
    WHERE  LastActive IS NOT NULL
)
INSERT INTO scores.OfficialPlayer (MixId, Username, AvatarUrl, UserId, UserIdSource, LastSeenAt)
SELECT  w.MixId, w.Tag, NULL, w.UserId, 'GameTag', SYSDATETIMEOFFSET()
FROM    Winner w
WHERE   w.Seat = 1
  AND   NOT EXISTS (
            SELECT 1 FROM scores.OfficialPlayer op
            WHERE op.MixId = w.MixId AND {NormalizedUsername} = UPPER(w.Tag));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only rows this migration created or claimed carry the marker, so the reversal is
            // exact and import-observed links are untouched. Rows that have since placed are
            // real board players now and stay — they just lose the backfilled link.
            migrationBuilder.Sql(@"
DELETE op
FROM   scores.OfficialPlayer op
WHERE  op.UserIdSource = 'GameTag'
  AND  NOT EXISTS (SELECT 1 FROM scores.OfficialLeaderboardPlacement p WHERE p.PlayerId = op.Id);

UPDATE scores.OfficialPlayer
SET    UserId = NULL, UserIdSource = 'None'
WHERE  UserIdSource = 'GameTag';
");
        }
    }
}
