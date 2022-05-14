using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class ChartAttempt
{
    public ChartAttempt(LetterGrade letterGrade, bool isBroken, Score? score)
    {
        LetterGrade = letterGrade;
        IsBroken = isBroken;
        Score = score;
    }

    public LetterGrade LetterGrade { get; }
    public bool IsBroken { get; }
    public Score? Score { get; }
}