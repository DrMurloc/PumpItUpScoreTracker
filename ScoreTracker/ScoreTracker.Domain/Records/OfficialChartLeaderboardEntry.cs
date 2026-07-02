using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record OfficialChartLeaderboardEntry(string Username, Chart Chart, PhoenixScore Score, Uri AvatarUrl)
    {
    }
}
