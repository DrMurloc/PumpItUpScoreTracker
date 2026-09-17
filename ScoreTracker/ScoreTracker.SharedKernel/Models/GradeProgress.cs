using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.SharedKernel.Models;

/// <summary>
///     Where a score sits inside its letter grade on one mix: the grade, the grade above it, the
///     points still to go to that grade's floor, and how far through the current grade the score
///     already is (0 at the grade's floor, approaching 1 just under the next one).
///     <para>
///         SSS+ has no grade above it, so its next line is the Perfect Game: <see cref="NextGrade" />
///         is null and <see cref="PointsToNext" /> counts to 1,000,000. A Perfect Game itself has
///         nothing left to reach — zero points to go, all the way through.
///     </para>
/// </summary>
public readonly record struct GradeProgress(
    PhoenixLetterGrade Grade,
    PhoenixLetterGrade? NextGrade,
    int PointsToNext,
    double ShareThrough)
{
    public bool IsPerfectGame => PointsToNext == 0;

    public static GradeProgress Of(PhoenixScore score, MixEnum mix)
    {
        var grade = score.LetterGradeFor(mix);
        var floor = (int)grade.GetMinimumScoreFor(mix);
        var reachesForPerfectGame = grade == PhoenixLetterGrade.SSSPlus;
        var line = reachesForPerfectGame ? (int)PhoenixScore.Max : (int)grade.GetMaximumScoreFor(mix) + 1;
        PhoenixLetterGrade? next = reachesForPerfectGame ? null : PhoenixScore.From(line).LetterGradeFor(mix);

        return new GradeProgress(grade, next, line - (int)score, ((int)score - floor) / (double)(line - floor));
    }
}
