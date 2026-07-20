using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.OfficialMirror.Domain;

/// <summary>
///     One comparable ladder for world-first bands: every letter grade in enum order with
///     the perfect game one rung above SSS+. A score bands in the table of the mix it was
///     earned in, so a Phoenix AA and a Phoenix 2 AA rank equal even though their floors
///     differ — that is what makes cross-mix "was this band ever claimed" answerable.
/// </summary>
internal static class GradeBandLadder
{
    public const string PgBand = "PG";
    private static readonly int PgRank = Enum.GetValues<PhoenixLetterGrade>().Length + 1;

    public static (int Rank, string Name) Of(int score, MixEnum mix)
    {
        if (score >= 1_000_000) return (PgRank, PgBand);
        var grade = PhoenixScore.From(score).LetterGradeFor(mix);
        return ((int)grade + 1, grade.GetName());
    }

    public static int RankOf(string? band)
    {
        if (band == null) return 0;
        if (band == PgBand) return PgRank;
        var grade = PhoenixLetterGradeHelperMethods.TryParse(band);
        return grade == null ? 0 : (int)grade.Value + 1;
    }
}
