namespace ScoreTracker.PlayerProgress.Contracts;

/// <summary>
///     The Hot Streak category's knobs. PeerPercentile is the standout bar: a seed's best
///     score must beat this percent of Peers' scores on the chart (0 turns the bar — and
///     its cohort reads — off entirely; the improver flag alone qualifies the seed).
///     LookbackDays bounds how far back seeds are drawn from; null means all time.
///     IncludeOutdatedScores lets charts whose score is an age outlier in the player's own
///     record count as unplayed targets.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record HotStreakOptions(
    int PeerPercentile = 80,
    int? LookbackDays = 30,
    bool IncludeOutdatedScores = false);
