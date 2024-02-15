using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartSkillsRecord(Guid ChartId, IEnumerable<Name> Skills)
    {
    }
}
