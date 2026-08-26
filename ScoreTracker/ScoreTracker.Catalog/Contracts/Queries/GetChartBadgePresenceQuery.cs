using ScoreTracker.SharedKernel.Enums;
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
///     <para>
///         Takes a mix because presence is folder-relative: what counts as "really carrying"
///         a technique depends on how many charts around it do. Brackets sit on 14% of S14 and
///         79% of D26, so the same coverage is remarkable in one folder and unremarkable in
///         the other.
///     </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetChartBadgePresenceQuery(IReadOnlyList<Guid> ChartIds, MixEnum Mix)
    : IQuery<IReadOnlyDictionary<Guid, IReadOnlyList<ChartBadgePresenceRecord>>>;
