using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     Reduces our stored records to the same census shape piugame publishes, so the two can be
///     subtracted. Only PASSES count: the play-data page has no notion of a stage break, and a
///     player who imports breaks would otherwise read as permanently ahead of the site.
/// </summary>
internal static class LocalCensusBuilder
{
    public static AccountCensus Build(MixEnum mix, IEnumerable<RecordedPhoenixScore> records,
        IReadOnlyDictionary<Guid, Chart> charts, IReadOnlyCollection<string> offeredBuckets)
    {
        var buckets = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            // A break is not a pass, and a record with no score is a manual placeholder that the
            // official site has no counterpart for either.
            if (record.IsBroken || record.Score == null) continue;
            if (!charts.TryGetValue(record.ChartId, out var chart)) continue;

            var bucket = CensusBuckets.For(chart.Type, chart.Level, offeredBuckets);
            if (!buckets.TryGetValue(bucket, out var accumulator))
                buckets[bucket] = accumulator = new Accumulator();

            accumulator.Passes++;
            // The stored LetterGrade is whatever mix wrote the row, so it is re-derived here
            // against the mix being checked — Phoenix 2 moved the A/A+/AA/AA+ floors.
            accumulator.Grades.Increment(CensusBands.GradeToken(record.Score.Value.LetterGradeFor(mix)));
            if (record.Plate != null) accumulator.Plates.Increment(CensusBands.PlateToken(record.Plate.Value));
        }

        return new AccountCensus(mix,
            buckets.ToDictionary(kv => kv.Key,
                kv => new CensusBucket(kv.Key, kv.Value.Passes, kv.Value.Grades, kv.Value.Plates),
                StringComparer.Ordinal),
            0);
    }

    /// <summary>
    ///     Our PUMBILITY for the same records the census just read. Mirrors
    ///     <c>PlayerRatingSaga.RecalculateCore</c> — one merged top-50 across both types, breaks
    ///     and CO-OP excluded, Phoenix 2 pricing the plate — deliberately rather than reading the
    ///     stored PlayerStats value: the rating sweep runs asynchronously after an import, so the
    ///     stored number can still be the pre-import one at the moment a check reports.
    /// </summary>
    public static double Pumbility(MixEnum mix, IEnumerable<RecordedPhoenixScore> records,
        IReadOnlyDictionary<Guid, Chart> charts)
    {
        var scoring = ScoringConfiguration.PumbilityScoring(mix, true);
        var rated = new List<double>();
        foreach (var record in records)
        {
            if (record.IsBroken || record.Score == null) continue;
            if (!charts.TryGetValue(record.ChartId, out var chart)) continue;
            if (chart.Type == ChartType.CoOp) continue;

            rated.Add(mix == MixEnum.Phoenix2
                ? scoring.GetScore(chart.Type, chart.Level, record.Score.Value,
                    record.Plate ?? PhoenixPlate.RoughGame, record.IsBroken)
                : scoring.GetScore(chart.Type, chart.Level, record.Score.Value));
        }

        return rated.OrderByDescending(r => r).Take(50).Sum();
    }

    private sealed class Accumulator
    {
        public int Passes { get; set; }
        public Dictionary<string, int> Grades { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, int> Plates { get; } = new(StringComparer.Ordinal);
    }

    private static void Increment(this IDictionary<string, int> counts, string key)
    {
        counts[key] = counts.TryGetValue(key, out var existing) ? existing + 1 : 1;
    }
}

/// <summary>
///     The site's own tokens for a grade and a plate. Comparing histograms means agreeing on the
///     key, and the site's spelling is the one that cannot be changed.
/// </summary>
internal static class CensusBands
{
    private static readonly IReadOnlyDictionary<PhoenixLetterGrade, string> GradeTokens =
        new Dictionary<PhoenixLetterGrade, string>
        {
            [PhoenixLetterGrade.SSSPlus] = "SSS_PLUS",
            [PhoenixLetterGrade.SSS] = "SSS",
            [PhoenixLetterGrade.SSPlus] = "SS_PLUS",
            [PhoenixLetterGrade.SS] = "SS",
            [PhoenixLetterGrade.SPlus] = "S_PLUS",
            [PhoenixLetterGrade.S] = "S",
            [PhoenixLetterGrade.AAAPlus] = "AAA_PLUS",
            [PhoenixLetterGrade.AAA] = "AAA",
            [PhoenixLetterGrade.AAPlus] = "AA_PLUS",
            [PhoenixLetterGrade.AA] = "AA",
            [PhoenixLetterGrade.APlus] = "A_PLUS",
            [PhoenixLetterGrade.A] = "A",
            [PhoenixLetterGrade.B] = "B",
            [PhoenixLetterGrade.C] = "C",
            [PhoenixLetterGrade.D] = "D",
            [PhoenixLetterGrade.F] = "F"
        };

    public static string GradeToken(PhoenixLetterGrade grade)
    {
        return GradeTokens.TryGetValue(grade, out var token) ? token : grade.ToString().ToUpperInvariant();
    }

    public static string PlateToken(PhoenixPlate plate)
    {
        return plate.GetShorthand().ToLowerInvariant();
    }
}
