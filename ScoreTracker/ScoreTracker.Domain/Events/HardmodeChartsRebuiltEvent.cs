using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.Events
{
    /// <summary>
    ///     The weekly Hardmode census has written a new chart list, and everything priced against
    ///     it is now stale (docs/design/hardmode-leaderboard.md §8).
    ///     <para>
    ///         Lives in Domain rather than ChartIntelligence's contracts because its two consumers
    ///         sit in verticals that cannot both reference the publisher: PlayerProgress writes the
    ///         site accounts' pool totals, OfficialMirror writes the board players'. Domain is the
    ///         one place both can see.
    ///     </para>
    ///     <para>
    ///         Every field is a primitive, so it survives the transport's own serializer — an
    ///         opaque value type here would arrive as <c>default</c> with no error anywhere
    ///         (CLAUDE.md, bus message serialization).
    ///     </para>
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record HardmodeChartsRebuiltEvent(MixEnum Mix, int QualifyingCharts, int PoolsCounted);
}
