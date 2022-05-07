using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Models;

public sealed class ChartAttempt
{
    public ChartAttempt(LetterGrade letterGrade, bool isBroken)
    {
        LetterGrade = letterGrade;
        IsBroken = isBroken;
    }

    public LetterGrade LetterGrade { get; }
    public bool IsBroken { get; }
}