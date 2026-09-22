using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Where a score sits inside its letter grade on one mix: the grade, the grade above it, the
///     points already past the grade's floor and the points still to go to the next one.
///     <para>
///         SSS+ has no grade above it, so its next line is the Perfect Game: <see cref="NextGrade" />
///         is null and <see cref="PointsToNext" /> counts to 1,000,000. A Perfect Game itself has
///         nothing left to reach — zero points to go, all the way through.
///     </para>
/// </summary>
public readonly record struct GradeProgress(
    PhoenixLetterGrade Grade,
    PhoenixLetterGrade? NextGrade,
    int PointsIntoGrade,
    int PointsToNext)
{
    /// <summary>The grade's width in points: its floor to the next line.</summary>
    public int GradeWidth => PointsIntoGrade + PointsToNext;

    /// <summary>0 at the grade's floor, approaching 1 just under the next line, 1 for a Perfect Game.</summary>
    public double ShareThrough => PointsIntoGrade / (double)GradeWidth;

    public bool IsPerfectGame => PointsToNext == 0;

    public static GradeProgress Of(PhoenixScore score, MixEnum mix)
    {
        var grade = score.LetterGradeFor(mix);
        var floor = (int)grade.GetMinimumScoreFor(mix);
        // The top of the mix's ladder reaches for the perfect game — SSS+ on a Phoenix mix, SSS on
        // Rise, whose ladder has no plus tiers — rather than the enum's top value, which a mix
        // may not have; asking for the score above 1,000,000 would throw.
        var reachesForPerfectGame = (int)grade.GetMaximumScoreFor(mix) == (int)PhoenixScore.Max;
        var line = reachesForPerfectGame ? (int)PhoenixScore.Max : (int)grade.GetMaximumScoreFor(mix) + 1;
        PhoenixLetterGrade? next = reachesForPerfectGame ? null : PhoenixScore.From(line).LetterGradeFor(mix);

        return new GradeProgress(grade, next, (int)score - floor, line - (int)score);
    }
}
