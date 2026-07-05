using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Domain;

internal static class ScoreRankings
{
    /// <summary>
    ///     Tie-inclusive share of the cohort's scores at or below yours, cohort sorted
    ///     ascending. Tie-inclusive matters: a Perfect Game can't be beaten but can be
    ///     tied, so it must rank 1.0 even when the whole cohort has the PG. Mirrors
    ///     ScoreQualitySaga's ranking semantics.
    /// </summary>
    public static double TieInclusivePercentile(PhoenixScore[] ascendingCohort, PhoenixScore score)
    {
        if (ascendingCohort.Length == 0) return 1.0;
        var index = ascendingCohort.Select((s, i) => (s, i))
            .FirstOrDefault(k => k.s > score, (0, -1)).i;
        return index == -1 ? 1.0 : index / (double)ascendingCohort.Length;
    }
}
