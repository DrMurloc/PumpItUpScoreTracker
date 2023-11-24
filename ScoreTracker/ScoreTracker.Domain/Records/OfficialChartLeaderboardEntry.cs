using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record OfficialChartLeaderboardEntry(string Username, Chart Chart, PhoenixScore Score)
    {
    }
}
