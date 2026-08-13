using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

// Single-mix per message: replaying one mix's recompute never touches the other's.
[ExcludeFromCodeCoverage]
public sealed record ProcessPumbilityTierListCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
