using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

// Single-mix per message (plan doc): replaying one mix's recompute never touches the other's.
[ExcludeFromCodeCoverage]
public sealed record RecalculateChartLetterDifficultiesCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
