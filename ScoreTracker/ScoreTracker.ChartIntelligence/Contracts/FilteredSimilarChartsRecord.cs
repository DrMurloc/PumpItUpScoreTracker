namespace ScoreTracker.ChartIntelligence.Contracts;

/// <summary>
///     What a filtered search found, and what it looked through to find it.
///     <see cref="ChartsCompared" /> is not a statistic — it is what turns "1 match" from a
///     bug report into a sentence: *"Compared 30 charts by SPHAM within 2 levels — 1
///     match."* A filtered search that finds nothing must still be able to say what it
///     searched, which is why the count is the reader's filter selection rather than
///     whatever survived scoring.
///     <see cref="Matches" /> is every compared chart that scored, best first and unfiltered
///     by quality; the reader applies its own bar and shows the rest as near-misses.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record FilteredSimilarChartsRecord(
    IReadOnlyList<ChartSimilarityRecord> Matches,
    int ChartsCompared);
