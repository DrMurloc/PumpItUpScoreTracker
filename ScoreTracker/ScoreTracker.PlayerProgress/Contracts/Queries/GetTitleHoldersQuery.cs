using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Queries;

/// <summary>
///     Who holds one title, for the detail drawer. Read on demand rather than with the page:
///     the list is 213 titles deep in Phoenix and 272 in Phoenix 2, and a player opens one.
/// </summary>
/// <param name="Ladder">
///     Every title on the same ladder, weakest rung first, when this title sits on one. Naming
///     all of them turns "everybody who has ever held this" into "everybody standing here" —
///     on a low rung that is the difference between a list of the whole site and a useful one.
///     Empty for a title with no ladder behind it.
/// </param>
[ExcludeFromCodeCoverage]
public sealed record GetTitleHoldersQuery(MixEnum Mix, Name Title, IReadOnlyList<Name> Ladder)
    : IQuery<TitleHoldersRecord>
{
    public GetTitleHoldersQuery(MixEnum mix, Name title) : this(mix, title, Array.Empty<Name>())
    {
    }
}
