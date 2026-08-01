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
    }

    [ExcludeFromCodeCoverage]
    public sealed record OfficialPlacementReading(int Place, int BoardDepth, DateTimeOffset AsOf);
}
