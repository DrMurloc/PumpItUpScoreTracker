namespace ScoreTracker.Web.Services;

/// <summary>
///     How far a stage-broken run got, as the one number a player wants from it: the notes the
///     card judged over the chart's note count, as a whole percentage. Null whenever either side
///     is unknown — no breakdown (a best-list stage break carries none), no catalog count — so
///     the row falls back to the plain phrase rather than inventing a figure.
/// </summary>
public static class StageBreakProgress
{
    public static int? PercentIn(int? judgedNotes, int? chartNoteCount)
    {
        if (judgedNotes is not { } judged || chartNoteCount is not { } notes || notes <= 0 || judged < 0) return null;

        // Never over the top: a catalog count that lags a re-step could put the sum past it, and
        // "104% in" is a lie about the chart, not a fact about the run.
        return Math.Min(100, (int)Math.Round(100.0 * judged / notes, MidpointRounding.AwayFromZero));
    }
}
