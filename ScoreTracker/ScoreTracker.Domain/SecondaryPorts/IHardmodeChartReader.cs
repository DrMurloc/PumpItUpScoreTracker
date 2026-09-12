using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Published read of the week's Hardmode chart list — the rarest slice of every folder,
    ///     written by ChartIntelligence's weekly census (docs/design/hardmode-leaderboard.md).
    ///     <para>
    ///         A port rather than a contract query because the list's consumers sit on the wrong
    ///         side of the vertical chain: ChartIntelligence references only Catalog, Data and
    ///         Domain, so OfficialMirror — which has to price its board players against the same
    ///         list — cannot reach a ChartIntelligence contract without a new project reference.
    ///         It is also how the next surface to want the list (a notification, a widget, the
    ///         player page) reads it without one (D13).
    ///     </para>
    /// </summary>
    public interface IHardmodeChartReader
    {
        /// <summary>
        ///     Every qualifying chart on the mix, as written by the last census. Empty until the
        ///     census has run — every caller treats that as "Hardmode is not live here" rather
        ///     than as an error, which is also what a mix with no census looks like.
        /// </summary>
        Task<IReadOnlyList<HardmodeChartEntry>> GetQualifyingCharts(MixEnum mix,
            CancellationToken cancellationToken);
    }

    /// <summary>
    ///     One qualifying chart and why it qualified. <paramref name="Points" /> is the weighted
    ///     hold count (51 − slot, summed over every full pool it sits in) and
    ///     <paramref name="Holders" /> counts each player once, so a chart can carry points from
    ///     three pools and one holder.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record HardmodeChartEntry(Guid ChartId, ChartType ChartType, int Level, double Points,
        int Holders, int FolderSize, int FolderCut);
}
