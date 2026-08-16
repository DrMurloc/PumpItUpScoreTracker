using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     Where PUMBILITY comes from across every full pool on a mix, per band of the total — what
///     the calculator's population section draws (docs/design/pumbility-calculator.md D9). Null
///     until the nightly PUMBILITY tier-list sweep has written the mix. The mix is required on
///     purpose: a page that shows one mix's formula must never read another mix's players.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPumbilityPoolCompositionQuery(MixEnum Mix) : IQuery<PumbilityPoolCompositionRecord?>;
