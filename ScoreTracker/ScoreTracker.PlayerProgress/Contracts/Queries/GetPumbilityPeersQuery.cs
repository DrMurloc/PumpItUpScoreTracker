using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Contracts.Queries
{
    /// <summary>
    ///     The current user's PUMBILITY peers of one chart type — the ids, for a surface that
    ///     ranks a chart's scores against them (the chart leaderboard's PUMBILITY peer scope,
    ///     docs/design/pumbility-overhaul.md D40). The sibling of
    ///     <see cref="GetCompetitivePlayersQuery" /> for the other peer pool. Empty on any mix but
    ///     Phoenix 2, and empty while the viewer's own pool of the type is short of fifty (D28).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record GetPumbilityPeersQuery(ChartType ChartType, MixEnum Mix = MixEnum.Phoenix)
        : IQuery<IReadOnlyCollection<Guid>>;
}
