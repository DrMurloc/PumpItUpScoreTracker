using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
    Task<IEnumerable<Name>> GetSongNames(CancellationToken cancellationToken = default);

    Task<Chart> GetChart(Name songName, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetCoOpCharts(CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(IEnumerable<Guid>? chartIds = default,
        CancellationToken cancellationToken = default);
}