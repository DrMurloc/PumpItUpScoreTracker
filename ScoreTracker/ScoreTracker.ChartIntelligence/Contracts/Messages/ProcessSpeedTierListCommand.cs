using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Messages;

/// <summary>
///     Rebuilds the Speed tier list for a mix (docs/design/chart-identity.md §2). Not a
///     recurring job: its input is the banked step analysis, which moves only when a piucenter
///     ingestion lands — so an ingestion triggers this, and /Admin can force it.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record ProcessSpeedTierListCommand(MixEnum Mix = MixEnum.Phoenix)
{
}
