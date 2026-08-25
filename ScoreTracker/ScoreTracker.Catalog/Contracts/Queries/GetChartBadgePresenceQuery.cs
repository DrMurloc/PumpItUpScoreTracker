using ScoreTracker.SharedKernel.Messaging;

namespace ScoreTracker.Catalog.Contracts.Queries;

/// <summary>
///     Every badge each chart really carries, with the weight its evidence is worth.
///     <para>
///         This is the un-capped counterpart to <see cref="GetChartIdentityQuery" />. Identity
///         picks the few chips a card can show; a reader summing a player's ability across a
///         folder needs every badge that is genuinely there, and it needs the presence rule
///         applied here rather than re-implemented against raw coverages.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartBadgePresenceQuery(IReadOnlyList<Guid> ChartIds)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>>;
