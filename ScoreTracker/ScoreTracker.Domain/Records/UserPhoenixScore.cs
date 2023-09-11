using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record UserPhoenixScore(Guid ChartId, Name UserName, PhoenixScore Score)
    {
    }
}