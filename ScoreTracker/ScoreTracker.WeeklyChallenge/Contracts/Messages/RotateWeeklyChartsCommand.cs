using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Messages;

// Single-mix per message (plan doc): each mix's board rotates independently; a mix with
// no charts no-ops gracefully in the consumer.
[ExcludeFromCodeCoverage]
public sealed record RotateWeeklyChartsCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
