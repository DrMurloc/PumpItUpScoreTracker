using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The Breakdown page's read of the viewer against the players holding their title
    ///     (docs/design/pumbility-overhaul.md D68). <paramref name="Pool" /> picks the ladder — the
    ///     merged [P.B] gems and their levels, or the [S] / [D] rungs — and is ignored on Phoenix 1,
    ///     whose cohort is the difficulty title stored for the player.
    /// </summary>
    /// <param name="Band">
    ///     The band to read, for a viewer who picked one. Null answers the viewer's own: their
    ///     [P.B] level where that level holds <see cref="PumbilityBand.MinimumForLevel" /> players
    ///     and the gem around it where it does not, their [S] / [D] rung, or their stored Phoenix 1
    ///     title.
    /// </param>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityTitleCohortQuery(Guid UserId, MixEnum Mix = MixEnum.Phoenix,
        PumbilityPool Pool = PumbilityPool.Total, Name? Band = null) : IQuery<PumbilityCohortRecord>;
}
