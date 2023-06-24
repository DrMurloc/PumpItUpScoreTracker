using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Models;

public sealed class XXChartAttempt
{
    public XXChartAttempt(XXLetterGrade letterGrade, bool isBroken, XXScore? score, DateTimeOffset recordedOn)
    {
        LetterGrade = letterGrade;
        IsBroken = isBroken;
        Score = score;
        RecordedOn = recordedOn;
    }

    public XXLetterGrade LetterGrade { get; }
    public bool IsBroken { get; }
    public XXScore? Score { get; }
    public DateTimeOffset RecordedOn { get; }
}