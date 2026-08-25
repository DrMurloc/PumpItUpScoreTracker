using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ScoreLedger.Contracts;

/// <summary>
///     What plays of one letter grade look like, measured: the mean judgement mix per 1,000
///     notes over every judgement-carrying, non-broken best whose score lands in the grade on
///     the queried mix (docs/design/phoenix-score-calculator.md D8). ComboPer1000 averages only
///     the rows whose combo was solvable; CombosMeasured says how many that was.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GradeJudgementSpread(
    PhoenixLetterGrade Grade,
    int Plays,
    double PerfectsPer1000,
    double GreatsPer1000,
    double GoodsPer1000,
    double BadsPer1000,
    double MissesPer1000,
    double ComboPer1000,
    int CombosMeasured);
