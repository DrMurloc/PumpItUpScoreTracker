/*
    Seeds the two forward-only captures onto DrMurloc's most recent session, so the new row
    treatments have something to render during a local field test.

    Both numbers are normally written at capture time and neither is backfilled (D34), so a
    session that already happened has them null and the row correctly shows a bare arrow and no
    gain pill. This fills them in for ONE session on a LOCAL database.

    ⚠ Local only. It writes values that capture would have computed rather than values capture
      did compute, which is exactly the thing the write-time doctrine exists to avoid.

    Run against the Aspire SQL container. Set @Tag if you are not DrMurloc.
*/

DECLARE @Tag NVARCHAR(100) = N'DrMurloc';

DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM scores.[User] WHERE Name = @Tag);
IF @UserId IS NULL
BEGIN
    RAISERROR('No user named %s — set @Tag to your game tag.', 16, 1, @Tag);
    RETURN;
END

-- The session the page opens on: most recent by activity, whatever mix it was.
DECLARE @SessionId UNIQUEIDENTIFIER = (
    SELECT TOP 1 h.SessionId
    FROM scores.ScoreHighlight h
    WHERE h.UserId = @UserId AND h.SessionId IS NOT NULL
    GROUP BY h.SessionId
    ORDER BY MAX(h.OccurredAt) DESC);

IF @SessionId IS NULL
BEGIN
    RAISERROR('No captured session for this player — import scores first.', 16, 1);
    RETURN;
END

/*
    The competitive baseline: the player's level for the chart's type as the batch opened.
    Seeded a quarter-level under what each score actually rates, so every improver row renders a
    plausible "+0.2" or so rather than a uniform number. CalculateFungScore is
    level + (score - 965000) / 17500, with a 1.008^(level-19) multiplier on Singles at 20+; the
    stored ScoringLevel is close enough for a seed.
*/
UPDATE h
SET CompetitiveBaseline = ROUND(ISNULL(h.ScoringLevel, h.Level) - 0.25, 2)
FROM scores.ScoreHighlight h
WHERE h.UserId = @UserId
  AND h.SessionId = @SessionId
  AND h.Flags & 16 = 16          -- CompetitiveImprover
  AND h.CompetitiveBaseline IS NULL;

/*
    The PUMBILITY gain, on the crowned charts only — a chart outside the top 50 gained nothing,
    and a badge there would misrepresent exactly what this feature exists to get right. Values
    fan out by level so the strip shows a range rather than one repeated number.
*/
UPDATE h
SET PumbilityGain = 20 + (h.Level * 4) % 130
FROM scores.ScoreHighlight h
WHERE h.UserId = @UserId
  AND h.SessionId = @SessionId
  AND h.Flags & 1 = 1            -- PumbilityTop50
  AND h.PumbilityGain IS NULL;

SELECT @SessionId AS SeededSession,
       SUM(CASE WHEN CompetitiveBaseline IS NOT NULL THEN 1 ELSE 0 END) AS WithBaseline,
       SUM(CASE WHEN PumbilityGain IS NOT NULL THEN 1 ELSE 0 END) AS WithGain,
       COUNT(*) AS RowsInSession
FROM scores.ScoreHighlight
WHERE UserId = @UserId AND SessionId = @SessionId;
