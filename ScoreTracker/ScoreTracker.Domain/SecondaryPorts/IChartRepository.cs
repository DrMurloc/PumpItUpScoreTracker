using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
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

    Task SetChartVideo(Guid id, Uri videoUrl, Name channelName, CancellationToken cancellationToken = default);
    Task UpdateSong(Name songName, Bpm bpm, CancellationToken cancellationToken = default);
    Task UpdateChart(Guid chartId, Name stepArtist, ISet<Name> skills, CancellationToken cancellationToken = default);
    Task<IEnumerable<SkillRecord>> GetSkills(CancellationToken cancellationToken = default);
    Task<IEnumerable<ChartSkillsRecord>> GetChartSkills(CancellationToken cancellationToken = default);
    Task CreateSkill(SkillRecord skill, CancellationToken cancellationToken = default);
    void ClearCache();
}