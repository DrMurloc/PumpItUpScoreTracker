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
    }

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
