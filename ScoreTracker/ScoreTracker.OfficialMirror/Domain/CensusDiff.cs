namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Subtracts our census from piugame's. Everything the completeness check concludes comes from
///     here, and nothing here touches the network or the database — the whole detection rule is a
///     pure function over two dictionaries.
/// </summary>
internal static class CensusDiff
{
    /// <summary>
    ///     Compares bucket by bucket. **Never on the whole-account total**: on a real 2,851-chart
    ///     Phoenix account the totals matched exactly while the account was short one chart at
    ///     level 18 and long one below level 10, so a total-only check reported "in sync" and was
    ///     wrong (docs/design/import-completeness-check.md §3.2).
    /// </summary>
    public static IReadOnlyList<CensusFinding> Compare(AccountCensus official, AccountCensus local)
    {
        var findings = new List<CensusFinding>();
        var buckets = official.Buckets.Keys.Concat(local.Buckets.Keys)
            .Where(b => !CensusBuckets.IsAggregate(b))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(b => b, StringComparer.Ordinal);

        foreach (var bucket in buckets)
        {
            var theirs = official.For(bucket);
            var ours = local.For(bucket);
            var delta = theirs.Passes - ours.Passes;

            if (delta > 0)
            {
                findings.Add(new CensusFinding(bucket, CensusFindingKind.Missing, delta));
                continue;
            }

            if (delta < 0)
            {
                findings.Add(new CensusFinding(bucket, CensusFindingKind.Extra, -delta));
                continue;
            }

            // Only once the counts agree does a histogram difference mean a stale score. While a
            // bucket is short, its bands are short too, and reporting both would count the same
            // charts twice under two different names.
            findings.AddRange(StaleBands(bucket, theirs, ours));
        }

        return findings;
    }

    /// <summary>
    ///     A band piugame has more of than we do, at matching totals, is a score we hold at an
    ///     older value that has since crossed into a better band. Grades are the finer signal and
    ///     win when the mix publishes them; Phoenix publishes none, so it falls back to plates.
    ///     A score that improved without leaving its band is invisible here — that is the deep
    ///     scan's job, and the copy says so.
    /// </summary>
    private static IEnumerable<CensusFinding> StaleBands(string bucket, CensusBucket theirs, CensusBucket ours)
    {
        var useGrades = theirs.Grades.Count > 0;
        var theirBands = useGrades ? theirs.Grades : theirs.Plates;
        var ourBands = useGrades ? ours.Grades : ours.Plates;
        if (theirBands.Count == 0) yield break;

        foreach (var (band, count) in theirBands.OrderBy(b => b.Key, StringComparer.Ordinal))
        {
            var mine = ourBands.TryGetValue(band, out var held) ? held : 0;
            if (count > mine)
                yield return new CensusFinding(bucket, CensusFindingKind.OutOfDate, count - mine, band, useGrades);
        }
    }

    /// <summary>
    ///     What the panel leads with. Missing beats out-of-date beats extra: a player who is short
    ///     charts wants to hear that first, and "you have more than piugame" is never the headline.
    /// </summary>
    public static CensusFindingKind? Headline(IReadOnlyCollection<CensusFinding> findings)
    {
        if (findings.Any(f => f.Kind == CensusFindingKind.Missing)) return CensusFindingKind.Missing;
        if (findings.Any(f => f.Kind == CensusFindingKind.OutOfDate)) return CensusFindingKind.OutOfDate;
        if (findings.Any(f => f.Kind == CensusFindingKind.Extra)) return CensusFindingKind.Extra;
        return null;
    }

    /// <summary>
    ///     Whether naming the charts behind a finding is cheaper through the count tile's own
    ///     modal or by paging the level's best-score list. Only a band-localised finding can use
    ///     the modal at all, and the modal is the WORSE page size — six rows against twelve — so
    ///     it wins only when the band it can filter to is much smaller than the level.
    ///     <para>
    ///         The modal serves a whole GRADE cell cumulatively ("A or better"), not just the band,
    ///         so the row count is <see cref="CensusBands.RowsInCell" />, never the finding's own
    ///         count. Plate cells are exact.
    ///     </para>
    /// </summary>
    public static bool PreferPlayLog(CensusFinding finding, CensusBucket officialBucket, int bucketChartCount)
    {
        if (finding.Band == null) return false;

        var rows = CensusBands.RowsInCell(officialBucket, finding.Band, finding.IsGradeBand);
        if (rows == 0) return false;

        var throughModal = (rows + 5) / 6;
        var throughBestList = (bucketChartCount + 11) / 12;
        return throughModal < throughBestList;
    }
}
