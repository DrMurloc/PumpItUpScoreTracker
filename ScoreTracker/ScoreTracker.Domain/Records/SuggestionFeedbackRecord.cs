using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record SuggestionFeedbackRecord(Name SuggestionCategory, Name FeedbackCategory, string Notes,
        bool ShouldHide, bool IsPositive, Guid ChartId)
    {
    }
}
