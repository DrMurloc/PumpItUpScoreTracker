using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
    Task<IEnumerable<Name>> GetSongNames(CancellationToken cancellationToken = default);
    Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsByDifficulty(DifficultyLevel difficultyLevel,
        CancellationToken cancellationToken = default);
}