using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The charts within reach that pose the *least* similar problem — the anchor's
///     opposite numbers, worst first. A novelty (owner, 2026-07-15: *"that'll just be used
///     for memes/fun"*), so it is not held to the shelf's standard: nothing is stored for
///     it and it does not need to be perfect, it needs to be funny and defensible.
///     Computed live because the graph banks the twenty **nearest** charts, and the
///     furthest are by construction the ones that ranking never keeps.
///     Fewer than <see cref="Count" /> come back when the reach holds fewer; empty when the
///     anchor has no step analysis or nothing in range to compare with.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetLeastSimilarChartsQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix, int Count = 6)
    : IQuery<IReadOnlyList<ChartSimilarityRecord>>;
