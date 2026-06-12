using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

/// <summary>
///     The Score Ledger's published read contract (ADR-001 D3 "pull"): every consumer
///     outside the Ledger reads scores through this, never through
///     <see cref="IPhoenixRecordRepository" /> (which becomes Ledger-internal at P5).
///     Methods are added per consumer migration — additive only.
/// </summary>
public interface IScoreReader
{
    /// <summary>A player's best attempt per chart.</summary>
    Task<IEnumerable<RecordedPhoenixScore>> GetBestScores(Guid userId, CancellationToken cancellationToken);

    /// <summary>Bulk read for analytics: every player's best attempt in a level×type folder.</summary>
    Task<IEnumerable<(Guid UserId, RecordedPhoenixScore Record)>> GetScores(ChartType chartType,
        DifficultyLevel level, CancellationToken cancellationToken);
}
