using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     The Season page (§11.2): one season — the live one when <see cref="SeasonId" /> is null —
///     with the boards the viewer's mix runs, each ranked, and the viewer's own standing on
///     each. Null when no season exists yet.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSeasonPageQuery(MixEnum Mix, Guid? SeasonId = null, Guid? ViewerId = null)
    : IQuery<MoMSeasonPage?>;
