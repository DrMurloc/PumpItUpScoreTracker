using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The chart within reach that poses the *least* similar problem — the anchor's
///     opposite number. A novelty (owner, 2026-07-15: *"that'll just be used for
///     memes/fun"*), so it is not held to the shelf's standard: nothing is stored for it
///     and it does not need to be perfect, it needs to be funny and defensible.
///     Computed live because the graph banks the twenty **nearest** charts, and the
///     furthest is by construction the one thing that ranking never keeps.
///     Null when the anchor has no step analysis or nothing in range to compare with.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetOppositeChartQuery(Guid ChartId, MixEnum Mix = MixEnum.Phoenix)
    : IQuery<ChartSimilarityRecord?>;
