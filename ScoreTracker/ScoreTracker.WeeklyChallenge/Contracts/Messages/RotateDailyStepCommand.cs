using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.WeeklyChallenge.Contracts.Messages;

// One mix per message (mirrors RotateWeeklyChartsCommand): the recurring job publishes one per
// supported mix each midnight ET; a mix with no charts no-ops in the consumer.
[ExcludeFromCodeCoverage]
public sealed record RotateDailyStepCommand(MixEnum Mix = MixEnum.Phoenix);
