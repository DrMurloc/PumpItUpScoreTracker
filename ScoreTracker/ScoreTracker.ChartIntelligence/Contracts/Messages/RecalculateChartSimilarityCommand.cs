using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

// Single-mix per message (RecalculateScoringDifficultyCommand precedent): replaying one
// mix's rebuild never touches the other's. Phoenix-only on the schedule until real
// Phoenix 2 score volume exists (the compute-job convention in docs/SCHEDULED-JOBS.md).
[ExcludeFromCodeCoverage]
public sealed record RecalculateChartSimilarityCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
