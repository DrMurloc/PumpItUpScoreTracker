using ScoreTracker.Domain.Records;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     Write port for the per-record Pumbility stats projection. The projection belongs to
///     Player Progression (sole writer: PlayerRatingSaga), not the Score Ledger — split out
///     of <see cref="IPhoenixRecordRepository" /> (rearch P5) so the Ledger port can go
///     vertical-internal without dragging Progression's write along.
/// </summary>
public interface IPhoenixRecordStatsRepository
{
    Task UpdateScoreStats(Guid userId, IEnumerable<PhoenixRecordStats> stats,
        CancellationToken cancellationToken = default);
}
