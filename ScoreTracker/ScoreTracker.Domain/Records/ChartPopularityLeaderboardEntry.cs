using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.Records
{
    public sealed record ChartPopularityLeaderboardEntry(Chart Chart, int Place, Uri SongImage)
    {
    }
}
