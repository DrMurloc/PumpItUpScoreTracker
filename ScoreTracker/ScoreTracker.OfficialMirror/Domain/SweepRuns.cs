namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     The rules a sweep run is judged by from outside the sweep: whether it is still working, and
///     which failed one can be finished.
/// </summary>
internal static class SweepRuns
{
    /// <summary>
    ///     The sweep checkpoints at least once per board, so a live run heartbeats every few seconds;
    ///     a run that has been silent this long is dead (killed process, deploy) and must not hold
    ///     the overlap lock.
    /// </summary>
    public static readonly TimeSpan HeartbeatWindow = TimeSpan.FromMinutes(15);

    /// <summary>The stage a run reaches once its whole download is written.</summary>
    public const string HighlightsStage = "Highlights";

    /// <summary>
    ///     How far a run must start after the latest finished week to become a week itself. The
    ///     leaderboards publish one week at a time, and a re-trigger two days after a finished week
    ///     would make a two-day week of it.
    /// </summary>
    public static readonly TimeSpan WeekSpacing = TimeSpan.FromDays(6);

    /// <summary>
    ///     The failed run to finish next, if any: the earliest one whose download completed — it died
    ///     working out its week —, that nothing is still working on, and that starts at least
    ///     <see cref="WeekSpacing" /> after the latest finished week. Weeks fill in order, so a later
    ///     run never jumps an earlier one: finishing a re-trigger first would strand the Sunday
    ///     before it.
    /// </summary>
    public static SnapshotRun? NextToFinish(IEnumerable<SnapshotRun> unfinished, SnapshotRun? latestFinished,
        DateTimeOffset now) =>
        unfinished
            .Where(run => run.CompletedAt == null
                          && run.Stage == HighlightsStage
                          && (run.Error != null || run.LastProgressAt < now - HeartbeatWindow)
                          && (latestFinished == null ||
                              run.StartedAt >= latestFinished.StartedAt + WeekSpacing))
            .OrderBy(run => run.StartedAt)
            .FirstOrDefault();
}
