/* =============================================================================================
   March of Murlocs slice 4b — a night to import, and a night the on-ramp will offer.

   Writes score-journal rows and the two sessions that hold them, for one player, so that:

     * /Player/{id}/Sessions shows tonight's run with the loud callout, because it holds a
       1:45 window of one chart type with well under fifty minutes of rest (D32); and
     * Submit's "Import recent plays" opens on that block, with yesterday's short run sitting
       above it behind a gap line, a stage break and a Singles play dimmed inside it, and one
       chart played twice so the repeat line has something to say (D45).

   Safe to re-run: everything it writes is tagged, and the first statement removes the previous
   run's rows. Writes nothing to any MoM table — the point is to record the session yourself.

   Set the two variables below, then run the whole file.
   ============================================================================================= */

SET NOCOUNT ON;

DECLARE @PlayerName  NVARCHAR(100) = N'DrMurloc';          -- whose journal to write into
DECLARE @MixId       UNIQUEIDENTIFIER = '1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B';  -- Phoenix
--       Phoenix 2 instead:              'A9B7D3C1-52E8-4F06-9B1A-2F8C33E01948'
DECLARE @Tag         NVARCHAR(100) = N'MOM-DEMO';          -- how a re-run finds its own rows
DECLARE @Charts      INT = 30;        -- Doubles charts in tonight's run
DECLARE @GapSeconds  INT = 45;        -- between the end of one song and the start of the next

DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM scores.[User] WHERE Name = @PlayerName);
IF @UserId IS NULL
BEGIN
    RAISERROR('No player called "%s" — set @PlayerName to your account name.', 16, 1, @PlayerName);
    RETURN;
END;

/* ---- clear the previous run --------------------------------------------------------------- */
DELETE j FROM scores.ScoreEventJournal j
    JOIN scores.ScoreSession s ON s.Id = j.SessionId
    WHERE s.AccountTag = @Tag AND s.UserId = @UserId;
DELETE FROM scores.ScoreSession WHERE AccountTag = @Tag AND UserId = @UserId;

/* ---- the charts ---------------------------------------------------------------------------
   Real charts from the catalog: Doubles 17 to 23, long enough to be worth points, with the
   duration that actually prices them. Ordered by id so a re-run picks the same thirty. */
DECLARE @Pool TABLE (rn INT, ChartId UNIQUEIDENTIFIER, Seconds INT, Level INT);
INSERT INTO @Pool (rn, ChartId, Seconds, Level)
SELECT TOP (@Charts)
       ROW_NUMBER() OVER (ORDER BY c.Id),
       c.Id,
       CAST(so.Duration / 10000000 AS INT),   -- Duration is stored in ticks
       cm.Level
FROM scores.Chart c
    JOIN scores.ChartMix cm ON cm.ChartId = c.Id AND cm.MixId = @MixId
    JOIN scores.Song so ON so.Id = c.SongId
WHERE c.Type = 'Double'
  AND cm.Level BETWEEN 17 AND 23
  AND so.Duration > 600000000            -- longer than a minute
ORDER BY c.Id;

IF (SELECT COUNT(*) FROM @Pool) < 10
BEGIN
    RAISERROR('Fewer than ten Doubles 17-23 charts with a duration in that mix — is the catalog populated?', 16, 1);
    RETURN;
END;

/* One Singles chart and one more Doubles chart, for the two rows the dialog has to dim and skip. */
DECLARE @SinglesChart UNIQUEIDENTIFIER = (
    SELECT TOP 1 c.Id FROM scores.Chart c
        JOIN scores.ChartMix cm ON cm.ChartId = c.Id AND cm.MixId = @MixId
    WHERE c.Type = 'Single' AND cm.Level BETWEEN 17 AND 23 ORDER BY c.Id);
DECLARE @BreakChart UNIQUEIDENTIFIER = (
    SELECT TOP 1 c.Id FROM scores.Chart c
        JOIN scores.ChartMix cm ON cm.ChartId = c.Id AND cm.MixId = @MixId
    WHERE c.Type = 'Double' AND cm.Level >= 24
      AND c.Id NOT IN (SELECT ChartId FROM @Pool) ORDER BY c.Id);

/* ---- when it happened ---------------------------------------------------------------------
   Tonight's run ends two hours ago; each play starts where the previous one finished plus the
   gap. Yesterday's short run sits a day earlier so the fifteen-minute split keeps them apart. */
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
DECLARE @RunSeconds INT = (SELECT SUM(Seconds) + (COUNT(*) - 1) * @GapSeconds FROM @Pool);
DECLARE @Start DATETIMEOFFSET = DATEADD(SECOND, -(@RunSeconds + 7200), @Now);

DECLARE @Tonight UNIQUEIDENTIFIER = NEWID();
DECLARE @Yesterday UNIQUEIDENTIFIER = NEWID();

/* Each play's offset: everything before it, songs and gaps both. */
DECLARE @Timed TABLE (rn INT, ChartId UNIQUEIDENTIFIER, PlayedAt DATETIMEOFFSET, Seconds INT, Level INT);
INSERT INTO @Timed (rn, ChartId, PlayedAt, Seconds, Level)
SELECT p.rn,
       p.ChartId,
       DATEADD(SECOND,
               ISNULL(SUM(p.Seconds + @GapSeconds) OVER (ORDER BY p.rn ROWS BETWEEN UNBOUNDED PRECEDING AND 1 PRECEDING), 0),
               @Start),
       p.Seconds,
       p.Level
FROM @Pool p;

/* ---- the sessions -------------------------------------------------------------------------- */
INSERT INTO scores.ScoreSession
    (Id, UserId, MixId, Source, AccountTag, CardId, StartedAt, LastActivityAt, ScoreCount, NewCount,
     UpscoreCount, ProcessedAt)
VALUES
    (@Yesterday, @UserId, @MixId, 'officialImport', @Tag, '01',
     DATEADD(DAY, -1, @Start), DATEADD(SECOND, 600, DATEADD(DAY, -1, @Start)), 2, 1, 1, @Now),
    (@Tonight, @UserId, @MixId, 'officialImport', @Tag, '01',
     @Start, DATEADD(SECOND, @RunSeconds, @Start), @Charts + 3, @Charts, 3, @Now);

/* ---- tonight: the run ---------------------------------------------------------------------
   Scores spread across the AAA-to-SSS band so the grades on the cards are not all the same. */
INSERT INTO scores.ScoreEventJournal
    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, IsStageBroken,
     IsBest, SessionId)
SELECT NEWID(), NEWID(), t.PlayedAt, 'officialImport', @MixId, @UserId, t.ChartId,
       950000 + (t.rn * 1637) % 48000,
       CASE WHEN (t.rn * 1637) % 48000 >= 20000 THEN 'MarvelousGame' ELSE 'SuperbGame' END,
       0, 0, 1, @Tonight
FROM @Timed t;

/* One chart played twice, the second time better: the repeat the entry row and the import's
   checks both have something to say about (D45). */
INSERT INTO scores.ScoreEventJournal
    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, IsStageBroken,
     IsBest, SessionId)
SELECT NEWID(), NEWID(), DATEADD(SECOND, 30, DATEADD(SECOND, t.Seconds, t.PlayedAt)),
       'officialImport', @MixId, @UserId, t.ChartId, 998112, 'MarvelousGame', 0, 0, 1, @Tonight
FROM @Timed t WHERE t.rn = 7;

/* A Singles play in the middle of a Doubles night: counted out, and the reason shown beside it. */
IF @SinglesChart IS NOT NULL
INSERT INTO scores.ScoreEventJournal
    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, IsStageBroken,
     IsBest, SessionId)
SELECT NEWID(), NEWID(), DATEADD(SECOND, 60, DATEADD(SECOND, t.Seconds, t.PlayedAt)),
       'officialImport', @MixId, @UserId, @SinglesChart, 964311, 'MarvelousGame', 0, 0, 1, @Tonight
FROM @Timed t WHERE t.rn = 12;

/* The stage break that ended the night: no score, and worth nothing (D40). */
IF @BreakChart IS NOT NULL
INSERT INTO scores.ScoreEventJournal
    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, IsStageBroken,
     IsBest, SessionId)
SELECT NEWID(), NEWID(), DATEADD(SECOND, 90, DATEADD(SECOND, t.Seconds, t.PlayedAt)),
       'officialImport', @MixId, @UserId, @BreakChart, NULL, NULL, 1, 1, 0, @Tonight
FROM @Timed t WHERE t.rn = @Charts;

/* ---- yesterday: two plays, a day earlier, so the split has a second block to draw ----------- */
INSERT INTO scores.ScoreEventJournal
    (Id, EventId, OccurredAt, Source, MixId, UserId, ChartId, Score, Plate, IsBroken, IsStageBroken,
     IsBest, SessionId)
SELECT NEWID(), NEWID(), DATEADD(DAY, -1, DATEADD(SECOND, (t.rn - 1) * 240, @Start)),
       'officialImport', @MixId, @UserId, t.ChartId, 921004 + t.rn * 3000, 'SuperbGame', 0, 0, 1, @Yesterday
FROM @Timed t WHERE t.rn <= 2;

/* ---- what landed --------------------------------------------------------------------------- */
SELECT 'tonight' AS Block,
       COUNT(*) AS Plays,
       MIN(j.OccurredAt) AS FirstPlay,
       MAX(j.OccurredAt) AS LastPlay,
       CAST(@RunSeconds / 60.0 AS DECIMAL(6, 1)) AS SpanMinutes,
       CAST((SELECT SUM(Seconds) FROM @Pool) / 60.0 AS DECIMAL(6, 1)) AS SongMinutes,
       CAST((105.0 - (SELECT SUM(Seconds) FROM @Pool) / 60.0) AS DECIMAL(6, 1)) AS RestInWindowMinutes
FROM scores.ScoreEventJournal j WHERE j.SessionId = @Tonight
UNION ALL
SELECT 'yesterday', COUNT(*), MIN(j.OccurredAt), MAX(j.OccurredAt), NULL, NULL, NULL
FROM scores.ScoreEventJournal j WHERE j.SessionId = @Yesterday;

/* Rest in the window under fifty minutes is what makes the callout appear. If SongMinutes comes
   out under 55, raise @Charts or drop @GapSeconds and run it again. */

/* ---- to remove everything this wrote -------------------------------------------------------
DELETE j FROM scores.ScoreEventJournal j
    JOIN scores.ScoreSession s ON s.Id = j.SessionId WHERE s.AccountTag = N'MOM-DEMO';
DELETE FROM scores.ScoreSession WHERE AccountTag = N'MOM-DEMO';
   ------------------------------------------------------------------------------------------- */
