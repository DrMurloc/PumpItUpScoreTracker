namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     The bars this vertical's per-competitive-level evidence must clear before anything
///     reads meaning into it.
///     Public because a verdict sentence and the graph it captions live on opposite sides of
///     the vertical boundary, and that is exactly how they drifted apart: the knee gated
///     thin buckets and the chart under it did not, so the page said "scores open up at 19"
///     over a line that started at 3.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ChartEvidenceThresholds
{
    /// <summary>
    ///     Scores a competitive-level bucket needs before its central tendency may be read
    ///     at all. One player is enough to BE a level's whole population, and at a
    ///     population of one the minimum, the average and the maximum are the same number
    ///     wearing three hats — an envelope drawn from that is noise with a confident line
    ///     through it.
    ///     A healthy bucket in the range that matters runs 15–90 players, so ten is a low
    ///     bar that still demands a population.
    /// </summary>
    public const int MinimumPerCompetitiveLevel = 10;

    /// <summary>
    ///     At or below this, a competitive level is a "not enough data" floor rather than a
    ///     skill — roughly 900 accounts sit here. They are unrated, not beginners, so they
    ///     cannot be placed on a competitive-level axis at all: left in, they scatter a
    ///     passer or two onto charts far above them and the left tail becomes a claim that
    ///     level-1 players clear D23s.
    /// </summary>
    public const double UnratedCompetitiveLevel = 1.0;
}
