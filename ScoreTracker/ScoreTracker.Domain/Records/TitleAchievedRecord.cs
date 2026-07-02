using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record TitleAchievedRecord(Guid UserId, Name Title, ParagonLevel ParagonLevel)
    {
    }
}
