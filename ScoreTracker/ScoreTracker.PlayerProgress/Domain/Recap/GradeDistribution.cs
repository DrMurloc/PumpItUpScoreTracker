using ScoreTracker.Domain.Models;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Domain.Recap;

/// <summary>
///     Pass counts per letter grade with the plus-grades folded into their base grade
///     (SS+ counts as SS) — ten buckets max instead of sixteen, best grade first.
/// </summary>
internal static class GradeDistribution
{
    public static IReadOnlyList<RecapGradeCount> Calculate(IEnumerable<RecordedPhoenixScore> passes)
    {
        return passes
            .Where(p => p is { IsBroken: false, Score: not null })
            .GroupBy(p => Collapse(p.Score!.Value.LetterGrade))
            .OrderByDescending(g => g.Key)
            .Select(g => new RecapGradeCount(g.Key, g.Count()))
            .ToArray();
    }

    public static PhoenixLetterGrade Collapse(PhoenixLetterGrade grade)
    {
        return grade switch
        {
            PhoenixLetterGrade.SSSPlus => PhoenixLetterGrade.SSS,
            PhoenixLetterGrade.SSPlus => PhoenixLetterGrade.SS,
            PhoenixLetterGrade.SPlus => PhoenixLetterGrade.S,
            PhoenixLetterGrade.AAAPlus => PhoenixLetterGrade.AAA,
            PhoenixLetterGrade.AAPlus => PhoenixLetterGrade.AA,
            PhoenixLetterGrade.APlus => PhoenixLetterGrade.A,
            _ => grade
        };
    }
}
