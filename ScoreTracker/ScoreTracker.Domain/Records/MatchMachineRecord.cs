using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    public sealed record MatchMachineRecord(Name MachineName, int Priority, bool IsWarmup)
    {
    }
}
