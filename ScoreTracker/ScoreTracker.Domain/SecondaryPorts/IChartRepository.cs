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

    /// <summary>
    ///     The same charts as one season prices them (docs/design/seasons.md D33, §4.3): each chart's
    ///     <see cref="Chart.Level" /> is its season rating where a <c>ChartSeason</c> row exists and
    ///     the printed level otherwise. The <paramref name="level" /> filter still selects by the
    ///     printed level — the folder a chart sits in does not move with its rating (§10.1) — so a
    ///     22 rated 21 for the season is returned with <c>Level == 21</c> when the 22 folder is asked
    ///     for, and not at all when the 21 folder is. A sibling rather than a defaulted parameter: a
    ///     Moq setup is an expression tree, and CS0854 forbids omitting an optional argument in one.
    /// </summary>
    Task<IEnumerable<Chart>> GetCharts(MixEnum mix, SeasonId season, DifficultyLevel? level = null,
        ChartType? type = null, IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<Name>> GetSongNames(MixEnum mix, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Every chart's level in every mix that carries it, as one flat ChartMix read — no
    ///     song or skill joins. This is what the cross-mix History needs; deriving it by
    ///     loading all ~30 full catalogs is orders of magnitude slower. Carries the mix's
    ///     judged note count too — the folder-baseline sweep derives each chart's per-mix hold
    ///     share from it, and it is the same row being read either way — and the patch the
    ///     row entered its mix in, when the row names one: a rerate in the History reads with
    ///     its date.
    /// </summary>
    Task<IReadOnlyList<(Guid ChartId, MixEnum Mix, int Level, int? NoteCount, VersionStamp? AddedIn)>>
        GetChartMixLevels(CancellationToken cancellationToken = default);

    Task<Chart> GetChart(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default);

    Task<IEnumerable<Chart>> GetChartsForSong(MixEnum mix, Name songName,
        CancellationToken cancellationToken = default);


    Task<IEnumerable<Chart>> GetCoOpCharts(MixEnum mix, CancellationToken cancellationToken = default);

    Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(IEnumerable<Guid>? chartIds = default,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateSong(Name name, Name koreanName, Uri imageUrl, SongType type, TimeSpan duration, Name songArtist,
        Bpm bpm,
        CancellationToken cancellationToken = default);

    /// <param name="addedInVersionId">
    ///     The patch of <paramref name="mix" /> the chart arrives in; null leaves it unknown
    ///     (docs/design/chart-versions.md §6).
    /// </param>
    Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl, Name stepArtist, Guid? addedInVersionId = null,
        CancellationToken cancellationToken = default);

    Task SetChartVideo(Guid id, Uri videoUrl, Name channelName, CancellationToken cancellationToken = default);
    Task UpdateSong(Name songName, Bpm bpm, CancellationToken cancellationToken = default);

    Task UpdateChart(Guid chartId, Name stepArtist,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Records an observed note count against the mix it was observed in. A chart's note
    ///     count is per-mix: the same chart can be re-stepped between mixes, which is the
    ///     whole reason /MixChanges can ask the question.
    /// </summary>
    Task UpdateNoteCount(MixEnum mix, Guid chartId, int noteCount, CancellationToken cancellationToken = default);

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