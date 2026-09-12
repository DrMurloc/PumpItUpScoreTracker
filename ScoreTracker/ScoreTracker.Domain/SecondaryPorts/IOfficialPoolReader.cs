using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Published read of the official-board players' PUMBILITY pools, rebuilt from the mirrored
    ///     per-chart boards (docs/design/hardmode-leaderboard.md §2).
    ///     <para>
    ///         The Hardmode census counts both populations, and board players are the larger half
    ///         of the Phoenix 2 electorate — but the census lives in ChartIntelligence, which
    ///         references only Catalog, Data and Domain and so cannot see OfficialMirror at all.
    ///         This is the same escape hatch <see cref="IDiscordFeedReader" /> is: the read crosses
    ///         through a Domain port instead of a project reference (D13).
    ///     </para>
    ///     <para>
    ///         ⚠ The mirrored chart boards only exist at level 20 and up, so every pool this
    ///         returns is level-20-plus by construction. That is a property of piugame's boards,
    ///         not of this port, and it is one of the two reasons the sub-20 census is thin.
    ///     </para>
    /// </summary>
    public interface IOfficialPoolReader
    {
        /// <summary>
        ///     One entry per (board player, pool kind) whose fifty the mirror can see in full, with
        ///     the charts in pool order. Board players already linked to a site account are
        ///     excluded — they vote once, through their own records, which carry plates the boards
        ///     never do.
        /// </summary>
        Task<IReadOnlyList<OfficialPoolSlots>> GetFullPools(MixEnum mix, CancellationToken cancellationToken);
    }

    /// <summary>
    ///     A board player's fifty for one pool kind. <paramref name="ChartIds" /> is in descending
    ///     value order, so index 0 is slot 1 and the slot weight is <c>51 − (index + 1)</c>.
    ///     <paramref name="ChartType" /> is null for the combined pool.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record OfficialPoolSlots(int OfficialPlayerId, ChartType? ChartType,
        IReadOnlyList<Guid> ChartIds);
}
