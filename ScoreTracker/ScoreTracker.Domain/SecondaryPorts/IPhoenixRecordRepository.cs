using ScoreTracker.Domain.Models;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IPhoenixRecordRepository
{
    Task UpdateBestAttempt(Guid userId, RecordedPhoenixScore score, CancellationToken cancellationToken = default);

    Task<IEnumerable<RecordedPhoenixScore>> GetRecordedScores(Guid userId,
        CancellationToken cancellationToken = default);

    Task<RecordedPhoenixScore?> GetRecordedScore(Guid userId, Guid chartId,
        CancellationToken cancellationToken = default);
}