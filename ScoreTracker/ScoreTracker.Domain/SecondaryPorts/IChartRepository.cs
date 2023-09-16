using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
    Task UpgradeSong(Name songName, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetCharts(MixEnum mix, DifficultyLevel? level = null, ChartType? type = null,
        IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Name>> GetSongNames(MixEnum mix, CancellationToken cancellationToken = default);

    Task<Chart> GetChart(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsForSong(MixEnum mix, Name songName,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetCoOpCharts(MixEnum mix, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(IEnumerable<Guid>? chartIds = default,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateSong(Name name, Uri imageUrl, SongType type, TimeSpan duration,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl,
        CancellationToken cancellationToken = default);

    Task SetSongDuration(Name songName, TimeSpan duration, CancellationToken cancellationToken = default);
    void ClearCache();
}