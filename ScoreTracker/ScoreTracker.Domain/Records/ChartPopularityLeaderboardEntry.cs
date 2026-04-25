using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record ChartPopularityLeaderboardEntry(Chart Chart, int Place, Uri SongImage)
    {
    }
}
