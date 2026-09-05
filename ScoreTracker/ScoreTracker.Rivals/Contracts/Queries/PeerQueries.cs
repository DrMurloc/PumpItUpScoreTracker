using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Rivals.Contracts.Queries;

/// <summary>
///     Where a player's scores stand among the peers they chose, one standing per chart they hold a
///     passing score on (docs/design/peers-abstraction.md §4.2). The subject defaults to the viewer
///     and reads their saved selection; a subject who is not the viewer is measured against the
///     competitive default (D19), because the peer choice is a personal preference and its rivals
///     and communities are the owner's to see.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetPeerStandingsQuery(MixEnum Mix, IReadOnlyCollection<Guid> ChartIds,
    Guid? SubjectUserId = null) : IQuery<IReadOnlyDictionary<Guid, PeerStanding>>;

/// <summary>
///     The viewer's peers as a list (D18): nearest competitive level first on
///     <paramref name="Dimension" /> (null = the combined level), capped at <paramref name="Take" />,
///     visibility applied, board-only rivals carried separately.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record GetMyPeerRosterQuery(MixEnum Mix, ChartType? Dimension, int Take) : IQuery<PeerRoster>;

/// <summary>Every source the viewer could tick, with the members each would contribute — for the Account dialog.</summary>
[ExcludeFromCodeCoverage]
public sealed record GetPeerSourceCatalogQuery(MixEnum Mix) : IQuery<PeerSourceCatalog>;
