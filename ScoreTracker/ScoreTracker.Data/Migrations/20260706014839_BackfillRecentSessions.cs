using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScoreTracker.Data.Migrations
{
    /// <summary>
    ///     Stamps SessionIds onto each player's LAST THREE journal clusters per mix so the
    ///     Sessions page has real, deep-linkable sessions from day one (owner call:
    ///     "everyone has something to start from"). Clustering mirrors the live Session
    ///     Batcher's rule — a new session starts after an 8-hour gap — over the journal's
    ///     OccurredAt timestamps (the June backfill seeded those from the records' real
    ///     RecordedDate values, so the clusters are genuine play days; unlike day
    ///     bucketing this keeps past-midnight sessions whole and splits double visits).
    ///     Older clusters stay unstamped and keep rendering as day buckets. Highlights
    ///     and milestones are deliberately NOT backfilled — flags are write-time truths.
    /// </summary>
    public partial class BackfillRecentSessions : Migration
    {
        // Public so the integration test can execute the exact production SQL against a
        // seeded database (the migration itself always runs against an empty journal in
        // test fixtures, so the behavior is asserted separately). The cluster and id
        // sets are MATERIALIZED into temp tables before the update — NEWID() inside a
        // CTE gets re-evaluated per joined row when the optimizer expands it, which
        // would hand every row of a session its own id.
        public const string Sql = """
            CREATE TABLE #clusters (
                Id uniqueidentifier PRIMARY KEY,
                UserId uniqueidentifier NOT NULL,
                MixId uniqueidentifier NOT NULL,
                SessionSeq int NOT NULL);

            WITH ordered AS (
                SELECT Id, UserId, MixId, OccurredAt,
                       CASE
                           WHEN LAG(OccurredAt) OVER (PARTITION BY UserId, MixId ORDER BY OccurredAt, Id) IS NULL
                                OR DATEDIFF(minute,
                                    LAG(OccurredAt) OVER (PARTITION BY UserId, MixId ORDER BY OccurredAt, Id),
                                    OccurredAt) > 480
                               THEN 1
                           ELSE 0
                       END AS StartsSession
                FROM [scores].[ScoreEventJournal]
                WHERE SessionId IS NULL
            )
            INSERT INTO #clusters (Id, UserId, MixId, SessionSeq)
            SELECT Id, UserId, MixId,
                   SUM(StartsSession) OVER (PARTITION BY UserId, MixId ORDER BY OccurredAt, Id
                       ROWS UNBOUNDED PRECEDING)
            FROM ordered;

            CREATE TABLE #sessionIds (
                UserId uniqueidentifier NOT NULL,
                MixId uniqueidentifier NOT NULL,
                SessionSeq int NOT NULL,
                SessionId uniqueidentifier NOT NULL,
                PRIMARY KEY (UserId, MixId, SessionSeq));

            INSERT INTO #sessionIds (UserId, MixId, SessionSeq, SessionId)
            SELECT UserId, MixId, SessionSeq, NEWID()
            FROM (
                SELECT UserId, MixId, SessionSeq,
                       DENSE_RANK() OVER (PARTITION BY UserId, MixId ORDER BY SessionSeq DESC) AS Recency
                FROM (SELECT DISTINCT UserId, MixId, SessionSeq FROM #clusters) AS distinctSessions
            ) AS ranked
            WHERE Recency <= 3;

            UPDATE j
            SET SessionId = i.SessionId
            FROM [scores].[ScoreEventJournal] j
            JOIN #clusters c ON c.Id = j.Id
            JOIN #sessionIds i
                ON i.UserId = c.UserId AND i.MixId = c.MixId AND i.SessionSeq = c.SessionSeq;

            DROP TABLE #sessionIds;
            DROP TABLE #clusters;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(Sql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only backfill: once organic sessions exist the stamped ids are
            // indistinguishable from real ones, so there is nothing safe to revert.
        }
    }
}
