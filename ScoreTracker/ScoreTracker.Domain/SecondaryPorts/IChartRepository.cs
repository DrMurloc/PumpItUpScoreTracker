using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Domain.SecondaryPorts;

public interface IChartRepository
{
    Task<IEnumerable<Chart>> GetCharts(MixEnum mix, DifficultyLevel? level = null, ChartType? type = null,
        IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Name>> GetSongNames(MixEnum mix, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Every chart's level in every mix that carries it, as one flat ChartMix read — no
    ///     song or skill joins. This is what the cross-mix History needs; deriving it by
    ///     loading all ~30 full catalogs is orders of magnitude slower.
    /// </summary>
    Task<IReadOnlyList<(Guid ChartId, MixEnum Mix, int Level)>> GetChartMixLevels(
        CancellationToken cancellationToken = default);

    Task<Chart> GetChart(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsForSong(MixEnum mix, Name songName,
        CancellationToken cancellationToken = default);


    Task<IEnumerable<Chart>> GetCoOpCharts(MixEnum mix, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(IEnumerable<Guid>? chartIds = default,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateSong(Name name, Name koreanName, Uri imageUrl, SongType type, TimeSpan duration, Name songArtist,
        Bpm bpm,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl, Name stepArtist,
        CancellationToken cancellationToken = default);

    Task SetChartVideo(Guid id, Uri videoUrl, Name channelName, CancellationToken cancellationToken = default);
    Task UpdateSong(Name songName, Bpm bpm, CancellationToken cancellationToken = default);

    Task UpdateChart(Guid chartId, Name stepArtist,
        CancellationToken cancellationToken = default);

    Task UpdateNoteCount(Guid chartId, int noteCount, CancellationToken cancellationToken = default);
    Task<IEnumerable<ChartSkillsRecord>> GetChartSkills(CancellationToken cancellationToken = default);
    Task SaveChartSkills(ChartSkillsRecord record, CancellationToken cancellationToken = default);

    Task SetSongCultureName(Name englishSongName, Name cultureCode, Name songName,
        CancellationToken cancellationToken = default);

    Task UpdateChartLetterDifficulties(IEnumerable<ChartLetterGradeDifficulty> difficulties,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartLetterGradeDifficulty>> GetChartLetterGradeDifficulties(IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default);

    Task<IDictionary<Name, Name>> GetEnglishLookup(Name cultureCode,
        CancellationToken cancellationToken);

    Task<IDictionary<Name, Name>> GetSongNames(Name cultureCode,
        CancellationToken cancellationToken);

    Task UpdateSongImage(Name songName, Uri newImage, CancellationToken cancellationToken = default);
    void ClearCache();
}