using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record SkillRecord(Name Name, string Description, string Color, string Category)
    {
    }
}
