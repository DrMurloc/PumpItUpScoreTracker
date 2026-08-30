using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Application;

/// <summary>
///     The Ledger's cache entries, shared rather than private to their readers — a format only the
///     reader knows is exactly what leaves an evicting writer guessing (the OfficialCacheKeys
///     precedent).
/// </summary>
internal static class LedgerCacheKeys
{
    /// <summary>
    ///     The charts carrying a limbo leaderboard, per mix. Read on every chart view to decide
    ///     whether the Lowest Passing chip renders, and written only by hand-run SQL — so there is
    ///     nothing to evict on and the TTL is short instead of long
    ///     (docs/design/limbo-leaderboard.md §5).
    /// </summary>
    public static string LimboCharts(MixEnum mix)
    {
        return $"LimboCharts__{mix}";
    }

    /// <summary>Five minutes: an INSERT lights its chip on the same visit, not the next restart.</summary>
    public static readonly TimeSpan LimboChartsTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    ///     One chart's limbo board. Long-lived because every journal write for the chart evicts it
    ///     — the TTL is the backstop, not the mechanism.
    /// </summary>
    public static string LimboBoard(MixEnum mix, Guid chartId)
    {
        return $"LimboBoard__{mix}__{chartId}";
    }

    public static readonly TimeSpan LimboBoardTtl = TimeSpan.FromHours(24);

    /// <summary>
    ///     The score calculator's per-level census of personal bests. One grouped read over the
    ///     whole record table; the section it feeds moves at the population's pace, so hours of
    ///     staleness are invisible and nothing evicts it.
    /// </summary>
    public static string ScorePopulation(MixEnum mix)
    {
        return $"ScorePopulation__{mix}";
    }

    public static readonly TimeSpan ScorePopulationTtl = TimeSpan.FromHours(6);

    /// <summary>The measured per-grade judgement spreads, on the same terms as the census.</summary>
    public static string JudgementSpreads(MixEnum mix)
    {
        return $"JudgementSpreads__{mix}";
    }

    public static readonly TimeSpan JudgementSpreadsTtl = TimeSpan.FromHours(6);

    /// <summary>
    ///     One chart's judged stage breaks — the failure rail. Imports append all day, so this
    ///     runs on a short TTL rather than eviction: a death showing up five minutes late is
    ///     invisible, a journal-write hook in every import path is not.
    /// </summary>
    public static string StageBreaks(MixEnum mix, Guid chartId)
    {
        return $"StageBreaks__{mix}__{chartId}";
    }

    public static readonly TimeSpan StageBreaksTtl = TimeSpan.FromMinutes(5);
}
