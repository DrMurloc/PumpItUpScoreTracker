using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.Domain.SecondaryPorts
{
    /// <summary>
    ///     Published read of how many times a chart was played before the play that cleared it,
    ///     within one session. Owned by ScoreLedger (it is a journal read), consumed by
    ///     PlayerProgress at capture time — and PlayerProgress sits UPSTREAM of ScoreLedger in
    ///     the reference graph (ScoreLedger → Communities → PlayerProgress), so a contract
    ///     query would close a cycle. This is the same escape hatch
    ///     <see cref="IDiscordFeedReader" /> exists for.
    /// </summary>
    public interface IScoreAttemptReader
    {
        /// <summary>
        ///     Charts absent from the result either never cleared in this session or cleared on
        ///     the first play; both mean "say nothing".
        /// </summary>
        Task<IReadOnlyDictionary<Guid, int>> GetSessionAttemptCounts(Guid userId, Guid sessionId,
            IReadOnlyList<Guid> chartIds, CancellationToken cancellationToken);
    }

    /// <summary>
    ///     Published read of where a score would place on a chart's official board. Owned by
    ///     OfficialMirror, consumed by PlayerProgress at capture time — same cycle, same reason
    ///     as <see cref="IScoreAttemptReader" /> (OfficialMirror → ScoreLedger → Communities →
    ///     PlayerProgress).
    /// </summary>
    public interface IOfficialPlacementReader
    {
        /// <summary>
        ///     Estimates against the last sealed snapshot — the board is swept weekly and has
        ///     not seen these scores, so every result carries the date it was measured against
        ///     and callers print it. Charts with no mirrored board, and scores outside its
        ///     depth, come back absent.
        /// </summary>
        Task<IReadOnlyDictionary<Guid, OfficialPlacementReading>> EstimatePlacements(MixEnum mix, Guid userId,
            IReadOnlyList<(Guid ChartId, int Score)> scores, CancellationToken cancellationToken);

        /// <summary>
        ///     One PUMBILITY board's values, highest first, for ranking a computed pool against.
        ///     Null when the mix has never swept that board — Phoenix publishes only the
        ///     combined one, so asking it for Singles is a legitimate miss, not a failure.
        /// </summary>
        Task<OfficialBoardReading?> GetPumbilityBoard(MixEnum mix, string boardName,
            CancellationToken cancellationToken);

        /// <summary>
        ///     The board's own players whose pool of a chart type sits in a window, from the last
        ///     sealed snapshot (docs/design/pumbility-overhaul.md D59). The per-type PUMBILITY board
        ///     publishes the same quantity the stats row holds, so a board player's membership needs
        ///     no estimate — which is the whole reason this read exists rather than a guess from
        ///     their scores. Null when the mix has never swept that board; Phoenix publishes only
        ///     the combined one, so asking it for Singles is a legitimate miss.
        /// </summary>
        Task<BoardPeerGroupReading?> GetBoardPeers(MixEnum mix, ChartType chartType, double minimumPool,
            double maximumPool, CancellationToken cancellationToken);

        /// <summary>
        ///     What those players scored, one row per player and chart: the highest placement they
        ///     hold across every mirrored snapshot, not only the latest. Falling off a board is not
        ///     evidence a score went away, so the best of what was ever published is the reading.
        ///     <para>
        ///         Bounded by chart type and level because a caller wants a band, and unbounded it
        ///         would carry every level the boards reach. Charts below roughly level 20 come back
        ///         near-empty whatever is asked for — those boards are packed with near-perfect
        ///         scores and a peer's play there is simply never published.
        ///     </para>
        /// </summary>
        Task<IReadOnlyList<BoardScoreReading>> GetBoardScores(MixEnum mix, ChartType chartType,
            IReadOnlyCollection<int> boardPlayerIds, int minimumLevel, int maximumLevel,
            CancellationToken cancellationToken);
    }

    /// <summary>
    ///     One window's worth of board players and the snapshot they were read from. The date rides
    ///     with them because every surface that prints a board player's standing prints how old it
    ///     is (docs/design/peers-abstraction.md D37).
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record BoardPeerGroupReading(DateTimeOffset AsOf, IReadOnlyList<BoardPeerReading> Peers);

    /// <summary>
    ///     A player on the official board: the id their placements are keyed on, the public tag they
    ///     are named by, and the pool the board publishes for them.
    /// </summary>
    /// <param name="AccountId">
    ///     The PIU Scores account this row speaks for, when the mirror may speak for one — null both
    ///     for a player with no account and for one the mirror will not claim (D61). A caller counts
    ///     a non-null id once, as that account, and never also as a board player.
    /// </param>
    [ExcludeFromCodeCoverage]
    public sealed record BoardPeerReading(int BoardPlayerId, string Tag, double Pool, Guid? AccountId);

    /// <summary>One board player's best published score on one chart.</summary>
    [ExcludeFromCodeCoverage]
    public sealed record BoardScoreReading(int BoardPlayerId, Guid ChartId, int Level, int Score);

    [ExcludeFromCodeCoverage]
    public sealed record OfficialPlacementReading(int Place, int BoardDepth, DateTimeOffset AsOf);

    /// <summary>
    ///     A rating board's values, highest first, and the snapshot they came from. Ranking is a
    ///     pure function of these two, which is what lets a pool be placed without the sweep
    ///     having seen it.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public sealed record OfficialBoardReading(DateTimeOffset AsOf, IReadOnlyList<decimal> DescendingValues)
    {
        /// <summary>
        ///     The place a pool would take. Ties share the better place, matching the Olympic
        ///     placement the sweep writes. A pool under the last entry ranks one past the
        ///     board's depth — <see cref="IsRanked" /> is how a caller tells "outside the top N"
        ///     from a real seat.
        /// </summary>
        public int PlaceFor(decimal pool)
        {
            var above = 0;
            while (above < DescendingValues.Count && DescendingValues[above] > pool) above++;
            return above + 1;
        }

        public bool IsRanked(decimal pool)
        {
            return PlaceFor(pool) <= DescendingValues.Count;
        }
    }

    /// <summary>The PUMBILITY board names the sweep writes.</summary>
    [ExcludeFromCodeCoverage]
    public static class OfficialPumbilityBoardNames
    {
        public const string Combined = "PUMBILITY";
        public const string Singles = "PUMBILITY Singles";
        public const string Doubles = "PUMBILITY Doubles";
    }
}
