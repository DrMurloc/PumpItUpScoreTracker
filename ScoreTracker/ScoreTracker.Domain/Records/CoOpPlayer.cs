using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record CoOpPlayer(Name Tag, Name? HighestCoOpTitle, Name? HighestStandardTitle)
    {
    }
}
