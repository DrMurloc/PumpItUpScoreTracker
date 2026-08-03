using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One <c>?lv=</c> bucket's worth of passes, split by grade and by plate. Both sides of the
///     completeness comparison — what piugame says and what we hold — are reduced to this same
///     shape so the diff never has to know which side it is looking at.
/// </summary>
internal sealed record CensusBucket(
    string Bucket,
    int Passes,
    IReadOnlyDictionary<string, int> Grades,
    IReadOnlyDictionary<string, int> Plates,
    /// <summary>How many charts the mix has in this bucket, when the official side stated it.
    /// Decides whether paging the level is cheaper than paging a cell; null on our side.</summary>
    int? CatalogTotal = null)
{
    public static CensusBucket Empty(string bucket)
    {
        return new CensusBucket(bucket, 0, new Dictionary<string, int>(), new Dictionary<string, int>());
    }
}

/// <summary>
///     A whole account's census for one mix, plus the official PUMBILITY the same read produced.
///     <paramref name="Buckets" /> is keyed by the site's own <c>?lv=</c> token.
/// </summary>
internal sealed record AccountCensus(
    MixEnum Mix,
    IReadOnlyDictionary<string, CensusBucket> Buckets,
    double Pumbility)
{
    public int TotalPasses => Buckets.Values.Sum(b => b.Passes);

    public CensusBucket For(string bucket)
    {
        return Buckets.TryGetValue(bucket, out var value) ? value : CensusBucket.Empty(bucket);
    }
}

internal enum CensusFindingKind
{
    /// <summary>piugame counts passes at this bucket that we do not hold.</summary>
    Missing,

    /// <summary>Same number of passes, different grade or plate spread — scores we hold are behind.</summary>
    OutOfDate,

    /// <summary>We hold more than piugame does. A CSV import, a manual entry, or a retired chart.</summary>
    Extra
}

/// <summary>
///     One difference the census found, localised as far as the data allows: always to a bucket,
///     and for an out-of-date finding also to the grade or plate cell that moved — which is what
///     lets the repair enumerate six rows instead of a whole level.
/// </summary>
internal sealed record CensusFinding(
    string Bucket,
    CensusFindingKind Kind,
    int Count,
    string? Band = null,
    bool IsGradeBand = false,
    /// <summary>
    ///     The actual charts behind the count, once a naming pass has read the level. Empty when
    ///     the finding was never named — "we hold more than piugame" never is, and neither is a
    ///     bucket whose level the site would not enumerate.
    /// </summary>
    IReadOnlyList<NamedChart>? Charts = null);

/// <summary>
///     One chart a check found, with the score piugame holds for it. "1 score missing" is a
///     support ticket; "Ugly duck Toccata S17, 996,408" is an answer.
/// </summary>
internal sealed record NamedChart(Guid ChartId, string Song, ChartType Type, int Level, int Score);
