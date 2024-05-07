using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartRecommendation(Name Category, Guid ChartId, string Explanation, string ChartDetails = "")
    {
    }
}
