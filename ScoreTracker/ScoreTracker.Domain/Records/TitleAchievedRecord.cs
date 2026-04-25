using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record TitleAchievedRecord(Guid UserId, Name Title, ParagonLevel ParagonLevel)
    {
    }
}
