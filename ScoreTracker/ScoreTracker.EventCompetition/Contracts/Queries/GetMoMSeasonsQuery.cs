using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.EventCompetition.Contracts.Queries;

/// <summary>
///     Every season newest first, with a line per board of the viewer's mix: how many sessions,
///     who won it, and how the viewer did (§11.8, D35).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMoMSeasonsQuery(MixEnum Mix, Guid? ViewerId = null) : IQuery<IReadOnlyList<MoMSeasonListing>>;
