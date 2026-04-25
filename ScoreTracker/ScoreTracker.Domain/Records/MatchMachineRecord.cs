using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.Records
{
    [ExcludeFromCodeCoverage]
    public sealed record MatchMachineRecord(Name MachineName, int Priority, bool IsWarmup)
    {
    }
}
