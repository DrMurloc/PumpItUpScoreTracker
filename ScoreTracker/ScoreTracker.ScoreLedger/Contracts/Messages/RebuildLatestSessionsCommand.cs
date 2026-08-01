namespace ScoreTracker.ScoreLedger.Contracts.Messages;

/// <summary>
///     Re-runs the capture pipeline over every player's single most recent session, so the
///     breakdown's newer sections have something to show on the day it ships
///     (docs/design/session-breakdown.md §4.4).
///     <para>
///         ⚠ A rebuild computes against <b>today's</b> state — current top 50, current folder
///         clears, current cohorts, the latest official snapshot — not the state at session
///         time. Everything captured going forward is write-time truth; these rows are "as of
///         the press". For a session that just happened the two are nearly identical, which is
///         exactly why the scope is one session and not history.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record RebuildLatestSessionsCommand;
