using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record TitleAchievedRecord(Name Title, ParagonLevel ParagonLevel)
    {
    }
}
