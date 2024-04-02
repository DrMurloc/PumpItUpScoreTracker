using ScoreTracker.Domain.Enums;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartSkillsRecord(Guid ChartId, IEnumerable<Skill> ContainsSkills,
        IEnumerable<Skill> HighlightsSkill)
    {
    }
}
