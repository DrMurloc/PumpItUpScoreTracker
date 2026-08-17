using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Identity.Contracts;

/// <summary>
///     One row of the site search's Players section: who they are, and on what basis the caller
///     may see them — the row glows green for a shared community, red for a rival, split for
///     both (docs/design/player-page-and-site-search.md D16). The game tag rides along because a
///     hit may have matched on it rather than the name.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PlayerSearchHit(
    Guid UserId,
    Name Name,
    Name? GameTag,
    Uri Avatar,
    Name? Country,
    PlayerVisibility Visibility);
