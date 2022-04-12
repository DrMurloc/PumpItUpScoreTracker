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

    public static bool operator >(ChartAttempt attempt1, ChartAttempt attempt2)
    {
        return attempt1.LetterGrade > attempt2.LetterGrade || (attempt1.LetterGrade == attempt2.LetterGrade &&
                                                               attempt1.IsBroken && !attempt2.IsBroken);
    }

    public static bool operator <(ChartAttempt attempt1, ChartAttempt attempt2)
    {
        return attempt2 > attempt1;
    }
}