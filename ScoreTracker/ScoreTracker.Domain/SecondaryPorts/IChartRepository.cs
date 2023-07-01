using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
    Task<IEnumerable<Chart>> GetCharts(MixEnum? mix = null, DifficultyLevel? level = null, ChartType? type = null,
        IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Name>> GetSongNames(MixEnum? mix = null, CancellationToken cancellationToken = default);

    Task<Chart> GetChart(Guid chartId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetCoOpCharts(CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(IEnumerable<Guid>? chartIds = default,
        CancellationToken cancellationToken = default);
}