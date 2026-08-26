using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Catalog.Domain;
using ScoreTracker.Catalog.Infrastructure.Entities;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Catalog.Infrastructure;

internal sealed class EFChartRepository : IChartRepository
{
    private readonly IMemoryCache _cache;
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFChartRepository(IMemoryCache cache, IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
        _cache = cache;
    }

    public async Task<IEnumerable<Name>> GetSongNames(MixEnum mix, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        return (await (from cm in database.ChartMix
            join c in database.Chart on cm.ChartId equals c.Id
            join s in database.Song on c.SongId equals s.Id
            where cm.MixId == mixId
            select s.Name).Distinct().ToArrayAsync(cancellationToken)).Select(Name.From);
    }

    public async Task<Chart> GetChart(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(mix, cancellationToken);
        return charts[chartId];
    }


    public async Task<IEnumerable<Chart>> GetChartsForSong(MixEnum mix, Name songName,
        CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(mix, cancellationToken);
        return charts.Values.Where(c => c.Song.Name == songName);
    }


    public async Task<IEnumerable<Chart>> GetCoOpCharts(MixEnum mix, CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(mix, cancellationToken);
        return charts.Values.Where(c => c.Type == ChartType.CoOp);
    }

    private const string VideoCacheKey = $"{nameof(EFChartRepository)}_{nameof(GetChartVideoInformation)}";

    public async Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(
        IEnumerable<Guid>? chartIds = default, CancellationToken cancellationToken = default)
    {
        if (!_cache.TryGetValue<IDictionary<Guid, ChartVideoInformation>>(VideoCacheKey, out var chartVideos)
            || chartVideos == null)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.Set<ChartVideoEntity>()
                .Select(v => new { v.ChartId, v.VideoUrl, v.ChannelName, v.Side })
                .ToArrayAsync(cancellationToken);
            // A stored side is the pair-validity marker: sides only ever exist on a URL held by
            // exactly one same-song singles pair, so the partner is simply the URL's other row —
            // no song or type joins needed at read time.
            var chartsOnUrl = rows.GroupBy(r => r.VideoUrl, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
            chartVideos = rows.ToDictionary(r => r.ChartId, r =>
            {
                // TryParse, not Parse: a hand-typed value in SQL must mute its own caption,
                // never take every video surface down with the whole-table cache rebuild.
                var side = r.Side != null && Enum.TryParse<VideoSide>(r.Side, true, out var parsed)
                    ? parsed
                    : default(VideoSide?);
                var partner = side != null && chartsOnUrl[r.VideoUrl].Length == 2
                    ? chartsOnUrl[r.VideoUrl].Single(o => o.ChartId != r.ChartId).ChartId
                    : default(Guid?);
                return new ChartVideoInformation(r.ChartId, new Uri(r.VideoUrl), r.ChannelName, side, partner);
            });

            // One key holds the whole table, so an empty read asserts "no chart anywhere has a
            // video" — true only of a database still being filled, and a fortnight's expiry
            // outlasts the filling: every chart then reports no video until an admin edit evicts
            // the key. An empty result is therefore left uncached and re-asked on the next call.
            if (chartVideos.Count > 0)
                _cache.Set(VideoCacheKey, chartVideos, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14)
                });
        }

        return chartIds != null
            ? chartIds.Where(id => chartVideos.ContainsKey(id)).Select(id => chartVideos[id])
            : chartVideos.Values;
    }

    public async Task<Guid> CreateSong(Name name, Name koreanName, Uri imageUrl, SongType type, TimeSpan duration,
        Name songArtist,
        Bpm bpm,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var newSong = new SongEntity
        {
            Id = Guid.NewGuid(),
            ImagePath = imageUrl.ToString(),
            Name = name,
            Type = type.ToString(),
            Duration = duration,
            Artist = songArtist,
            MinBpm = bpm.Min,
            MaxBpm = bpm.Max
        };
        await database.Song.AddAsync(newSong, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await database.Set<SongNameLanguageEntity>().AddAsync(new SongNameLanguageEntity
        {
            CultureCode = "ko-KR",
            EnglishSongName = name.ToString(),
            SongName = koreanName.ToString()
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return newSong.Id;
    }

    public async Task SetChartVideo(Guid id, Uri videoUrl, Name channelName,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.Set<ChartVideoEntity>().FirstOrDefaultAsync(c => c.ChartId == id, cancellationToken);
        var oldUrl = entity?.VideoUrl;
        if (entity == null)
        {
            await database.Set<ChartVideoEntity>().AddAsync(new ChartVideoEntity
            {
                ChartId = id,
                ChannelName = channelName,
                VideoUrl = videoUrl.ToString()
            }, cancellationToken);
        }
        else
        {
            entity.VideoUrl = videoUrl.ToString();
            entity.ChannelName = channelName;
        }

        await database.SaveChangesAsync(cancellationToken);

        // A save that keeps the URL (a channel fix) is not a registration event — the video
        // didn't change, so whatever sides exist stay exactly as they are.
        if (!string.Equals(oldUrl, videoUrl.ToString(), StringComparison.Ordinal))
            await ApplyVideoRegistration(database, id, oldUrl, videoUrl.ToString(), cancellationToken);
    }

    /// <summary>
    ///     Applies one video registration event's side effects, and only that event's
    ///     (docs/design/video-sides.md): sides are durable data, so nothing here re-derives a
    ///     side an earlier event or a hand audit stored. The edited chart's side clears (its
    ///     video changed), a same-song partner stranded on the old URL clears (that pair no
    ///     longer exists), and when the new URL now holds exactly one same-song singles pair,
    ///     that pair gets its one-time assignment.
    /// </summary>
    private static async Task ApplyVideoRegistration(ChartAttemptDbContext database, Guid chartId,
        string? oldUrl, string newUrl, CancellationToken cancellationToken)
    {
        var songId = await database.Chart.Where(c => c.Id == chartId).Select(c => (Guid?)c.SongId)
            .FirstOrDefaultAsync(cancellationToken);
        if (songId == null) return;

        if (oldUrl != null)
        {
            var stranded = await (from v in database.Set<ChartVideoEntity>()
                join c in database.Chart on v.ChartId equals c.Id
                where v.VideoUrl == oldUrl && c.SongId == songId && v.ChartId != chartId && v.Side != null
                select v).ToArrayAsync(cancellationToken);
            foreach (var row in stranded) row.Side = null;
        }

        var groupRows = await (from v in database.Set<ChartVideoEntity>()
            join c in database.Chart on v.ChartId equals c.Id
            where v.VideoUrl == newUrl
            select new { Video = v, c.SongId, c.Type, c.Level }).ToArrayAsync(cancellationToken);
        var edited = groupRows.Single(r => r.Video.ChartId == chartId);
        edited.Video.Side = null;

        var songGroup = groupRows.Where(r => r.SongId == songId).ToArray();
        var levels = await PairLevels(database,
            songGroup.Select(r => (r.Video.ChartId, r.Level)).ToArray(), cancellationToken);
        var sides = VideoSideAssigner.DecideSides(
            songGroup.Select(r => new VideoChart(r.Video.ChartId, Enum.Parse<ChartType>(r.Type),
                levels[r.Video.ChartId])).ToArray(),
            groupRows.Length);
        foreach (var row in songGroup)
            if (sides.TryGetValue(row.Video.ChartId, out var side))
                row.Video.Side = side.ToString();

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    ///     The levels a forming pair is compared on: the first modern mix carrying BOTH charts
    ///     — Phoenix 2, then Phoenix, then XX, the same rule the migration's backfill used —
    ///     never levels from two different mixes. Base levels are the fallback for a pair no
    ///     modern mix carries whole.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, int>> PairLevels(ChartAttemptDbContext database,
        IReadOnlyCollection<(Guid ChartId, int BaseLevel)> charts, CancellationToken cancellationToken)
    {
        var result = charts.ToDictionary(c => c.ChartId, c => c.BaseLevel);
        if (charts.Count != 2) return result;
        var ids = charts.Select(c => c.ChartId).ToArray();
        var mixLevels = await database.ChartMix
            .Where(cm => ids.Contains(cm.ChartId))
            .Select(cm => new { cm.ChartId, cm.MixId, cm.Level })
            .ToArrayAsync(cancellationToken);
        foreach (var mixId in new[] { MixIds.Phoenix2, MixIds.Phoenix, MixIds.XX })
        {
            var onMix = mixLevels.Where(m => m.MixId == mixId).ToArray();
            if (onMix.Select(m => m.ChartId).Distinct().Count() != 2) continue;
            foreach (var m in onMix) result[m.ChartId] = m.Level;
            return result;
        }

        return result;
    }

    public async Task UpdateSong(Name songName, Bpm bpm, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var nameString = songName.ToString();
        var song = await database.Song.SingleAsync(s => s.Name == nameString, cancellationToken);
        song.MinBpm = bpm.Min;
        song.MaxBpm = bpm.Max;
        await database.SaveChangesAsync(cancellationToken);
        ClearCache();
    }

    public async Task UpdateChart(Guid chartId, Name stepArtist,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var chart = await database.Chart.SingleAsync(c => c.Id == chartId, cancellationToken);
        chart.StepArtist = stepArtist;


        await database.SaveChangesAsync(cancellationToken);

        ClearCache();
    }

    public async Task UpdateNoteCount(MixEnum mix, Guid chartId, int noteCount,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        // The observed mix, not a hardcoded Phoenix. Writing every observation onto the
        // Phoenix row meant a Phoenix 2 note count could never be recorded against Phoenix 2
        // — so a re-step between the two was undetectable by construction.
        var mixId = MixIds.For(mix);
        var entity =
            await database.ChartMix.FirstOrDefaultAsync(c => c.ChartId == chartId && c.MixId == mixId,
                cancellationToken);
        if (entity == null) return;
        entity.NoteCount = noteCount;
        await database.SaveChangesAsync(cancellationToken);

        var cache = await GetAllCharts(mix, cancellationToken);
        if (cache.TryGetValue(chartId, out var chart))
            cache[chartId] = chart with { Id = chartId, NoteCount = noteCount };
    }


    public async Task SetSongCultureName(Name englishSongName, Name cultureCode, Name songName,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var englishString = englishSongName.ToString();
        var cultureString = cultureCode.ToString();
        var entity = await database.Set<SongNameLanguageEntity>().FirstOrDefaultAsync(
            n => n.CultureCode == cultureString && n.EnglishSongName == englishString, cancellationToken);
        if (entity == null)
            await database.Set<SongNameLanguageEntity>().AddAsync(new SongNameLanguageEntity
            {
                CultureCode = cultureCode,
                EnglishSongName = englishSongName,
                SongName = songName
            }, cancellationToken);
        else
            entity.SongName = songName;
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateChartLetterDifficulties(IEnumerable<ChartLetterGradeDifficulty> difficulties,
        CancellationToken cancellationToken = default)
    {
        var models = difficulties.ToArray();
        var chartIds = models.Select(c => c.ChartId).Distinct().ToArray();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);

        var entities = (await database.ChartLetterDifficulty.Where(c => chartIds.Contains(c.ChartId))
                .ToArrayAsync(cancellationToken))
            .ToDictionary(e => (e.ChartId, Enum.Parse<ParagonLevel>(e.LetterGrade)));
        var toAdd = new List<ChartLetterDifficultyEntity>();
        foreach (var model in models)
        foreach (var letter in model.Percentiles.Keys)
            if (entities.ContainsKey((model.ChartId, letter)))
            {
                entities[(model.ChartId, letter)].Percentile = model.Percentiles[letter];
                entities[(model.ChartId, letter)].WeightedSum = model.WeightedSum[letter];
            }
            else
            {
                toAdd.Add(new ChartLetterDifficultyEntity
                {
                    ChartId = model.ChartId,
                    LetterGrade = letter.ToString(),
                    Percentile = model.Percentiles[letter],
                    WeightedSum = model.WeightedSum[letter]
                });
            }

        await database.AddRangeAsync(toAdd, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<ChartLetterGradeDifficulty>> GetChartLetterGradeDifficulties(
        IEnumerable<Guid>? chartIds = null, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var charts = database.ChartLetterDifficulty.AsQueryable();
        if (chartIds != null) charts = charts.Where(c => chartIds.Contains(c.ChartId));

        return (await charts.ToArrayAsync(cancellationToken))
            .GroupBy(e => e.ChartId)
            .ToDictionary(e => e.Key, e => e.ToArray())
            .Select(kv => new ChartLetterGradeDifficulty(kv.Key,
                kv.Value.ToDictionary(e => Enum.Parse<ParagonLevel>(e.LetterGrade), e => e.Percentile),
                kv.Value.ToDictionary(e => Enum.Parse<ParagonLevel>(e.LetterGrade), e => e.WeightedSum)))
            .ToArray();
    }

    public async Task<IDictionary<Name, Name>> GetEnglishLookup(Name cultureCode, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync($"{nameof(EFChartRepository)}__SongNames__{cultureCode}__Reverse",
            async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(24);
                return (await GetSongNames(cultureCode, cancellationToken)).ToDictionary(kv => kv.Value, kv => kv.Key);
            });
    }

    public async Task<IDictionary<Name, Name>> GetSongNames(Name cultureCode,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync($"{nameof(EFChartRepository)}__SongNames__{cultureCode}", async o =>
        {
            o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(24);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var cultureString = cultureCode.ToString();
            return (await database.Set<SongNameLanguageEntity>().Where(s => s.CultureCode == cultureString)
                    .ToArrayAsync(cancellationToken)).Select(e => (Name.From(e.EnglishSongName), Name.From(e.SongName)))
                .ToDictionary(e => e.Item1, e => e.Item2);
        });
    }

    public async Task UpdateSongImage(Name songName, Uri newImage, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var nameString = songName.ToString();
        var song = await database.Song.SingleAsync(s => s.Name == nameString, cancellationToken);
        song.ImagePath = newImage.ToString();
        await database.SaveChangesAsync(cancellationToken);
        ClearCache();
    }


    public void ClearCache()
    {
        foreach (var mixId in new[] { MixIds.XX, MixIds.Phoenix, MixIds.Phoenix2 })
        {
            var key = ChartCacheKey(mixId);
            _cache.Remove(key);
        }

        _cache.Remove($"{nameof(EFChartRepository)}_{nameof(GetChartVideoInformation)}");
    }

    public async Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl, Name stepArtist,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var newChart = new ChartEntity
        {
            Id = Guid.NewGuid(),
            Level = level,
            SongId = songId,
            Type = type.ToString(),
            StepArtist = stepArtist,
            // The mix a chart is created for IS its debut. Left unset this column takes its
            // database default of Phoenix, which made every chart ever added through here
            // claim a Phoenix origin no matter which mix it belongs to.
            OriginalMixId = MixIds.For(mix),
            // Co-ops store the player count in Level, and readers treat the persisted
            // PlayerCount column as authoritative — it must be materialized here or the
            // chart reports a 1-player co-op.
            PlayerCount = type == ChartType.CoOp ? (int)level : 1
        };
        var newChartMix = new ChartMixEntity
        {
            ChartId = newChart.Id,
            Id = Guid.NewGuid(),
            Level = level,
            MixId = MixIds.For(mix)
        };
        var newChartVideo = new ChartVideoEntity
        {
            ChartId = newChart.Id,
            ChannelName = channelName,
            VideoUrl = videoUrl.ToString()
        };
        await database.Chart.AddAsync(newChart, cancellationToken);
        await database.ChartMix.AddAsync(newChartMix, cancellationToken);
        await database.Set<ChartVideoEntity>().AddAsync(newChartVideo, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        await ApplyVideoRegistration(database, newChart.Id, null, newChartVideo.VideoUrl, cancellationToken);
        return newChart.Id;
    }

    public async Task<IEnumerable<Chart>> GetCharts(MixEnum mix, DifficultyLevel? level = null,
        ChartType? type = null,
        IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default)
    {
        var result =
            (await GetAllCharts(mix, cancellationToken)).Values.AsEnumerable();
        if (chartIds != null)
        {
            var chartIdsArray = chartIds.ToArray();
            result = result.Where(r => chartIdsArray.Contains(r.Id));
        }

        if (level != null)
        {
            var levelInt = (int)level.Value;
            result = result.Where(c => c.Level == levelInt);
        }

        if (type != null)
        {
            var typeString = type.Value.ToString();
            result = result.Where(c => c.Type.ToString() == typeString);
        }

        return result;
    }

    public async Task<IReadOnlyList<(Guid ChartId, MixEnum Mix, int Level)>> GetChartMixLevels(
        CancellationToken cancellationToken = default)
    {
        return (await _cache.GetOrCreateAsync($"{nameof(EFChartRepository)}__ChartMixLevels", async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var rows = await database.ChartMix
                .Select(cm => new { cm.ChartId, cm.MixId, cm.Level })
                .ToArrayAsync(cancellationToken);
            return (IReadOnlyList<(Guid, MixEnum, int)>)rows
                .Select(r => (r.ChartId, MixIds.ToEnum(r.MixId), r.Level))
                .ToArray();
        }))!;
    }

    private static string ChartCacheKey(Guid mixId)
    {
        return $"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}_Mix:{mixId}";
    }

    private async Task<IDictionary<Guid, Chart>> GetAllCharts(MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        return await _cache.GetOrCreateAsync<IDictionary<Guid, Chart>>(ChartCacheKey(mixId), async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            // OriginalMix maps through MixIds, not Enum.Parse(Mix.Name): legacy mix names
            // ("Prex 3", "OBG SE") are display strings, not enum identifiers.
            return await (from cm in database.ChartMix
                    join c in database.Chart on cm.ChartId equals c.Id
                    join s in database.Song on c.SongId equals s.Id
                    where cm.MixId == mixId
                    select new Chart(c.Id, MixIds.ToEnum(c.OriginalMixId),
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type),
                        cm.Level, mix, c.StepArtist, cm.NoteCount,
                        new HashSet<Skill>(),
                        LegacySlotHelperMethods.ToNullableLegacySlot(cm.LegacySlot),
                        c.PlayerCount))
                .ToDictionaryAsync(c => c.Id, cancellationToken);
        });
    }
}
