using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.ChartIntelligence.Contracts.Queries;

/// <summary>
///     The "Community Rating" tier list lens: every chart in the mix, bucketed into
///     TierListCategory bands from the community difficulty-vote aggregates (the same
///     numbers the adjusted-rating display uses). The legacy-mix tier list surface —
///     seeded by the PumpoutBackfill hindsight votes, sharpened by real players voting
///     (docs/design/legacy-mixes.md). Charts with no votes read Unrecorded.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetCommunityTierListQuery(MixEnum Mix) : IQuery<IEnumerable<SongTierListEntry>>
{
}
