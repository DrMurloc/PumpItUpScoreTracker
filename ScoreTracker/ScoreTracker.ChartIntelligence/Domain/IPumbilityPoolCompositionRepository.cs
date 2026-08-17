using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Domain;

/// <summary>
///     Storage for the population section's per-band sums. Replace-mix semantics: one save swaps
///     every band row for the mix, because the sweep recomputes all of them at once and a band that
///     stopped existing (a renamed gem) must not linger.
/// </summary>
internal interface IPumbilityPoolCompositionRepository
{
    Task Save(PumbilityPoolCompositionRecord composition, CancellationToken cancellationToken);

    /// <summary>Null when the sweep has never written this mix — the page's "not built yet" state.</summary>
    Task<PumbilityPoolCompositionRecord?> Get(MixEnum mix, CancellationToken cancellationToken);
}
