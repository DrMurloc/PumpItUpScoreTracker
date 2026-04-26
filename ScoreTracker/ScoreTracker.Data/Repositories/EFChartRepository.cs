using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartRepository : IChartRepository
{
    //Will  need to refactor this if I ever support non prod environments
    //Mostly saving some tedious joins for now.
    private static readonly IDictionary<MixEnum, Guid> MixGuids = new Dictionary<MixEnum, Guid>
    {
        { MixEnum.XX, Guid.Parse("20F8CCF8-94B1-418D-B923-C375B042BDA8") },
        { MixEnum.Phoenix, Guid.Parse("1ABB8F5A-BDA3-40F0-9CE7-1C4F9F8F1D3B") }
    };

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
        var mixId = MixGuids[mix];
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

    public async Task UpdateScoreLevel(MixEnum mix, Guid chartId, double scoringLevel,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixGuids[mix];
        var chartMix = await database.ChartMix.Where(cm => cm.MixId == mixId && cm.ChartId == chartId)
            .FirstOrDefaultAsync(cancellationToken);
        if (chartMix != null)
        {
            chartMix.ScoringLevel = scoringLevel == 0 ? null : scoringLevel;
            await database.SaveChangesAsync(cancellationToken);
        }
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
        var chartVideos = await _cache.GetOrCreateAsync<IDictionary<Guid, ChartVideoInformation>>(VideoCacheKey,
            async entry =>
            {
                entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
                await using var database = await _factory.CreateDbContextAsync(cancellationToken);
                return await database.ChartVideo
                    .Select(c => new ChartVideoInformation(c.ChartId, new Uri(c.VideoUrl), c.ChannelName))
                    .ToDictionaryAsync(c => c.ChartId, cancellationToken);
            });

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
        await database.SongNameLanguage.AddAsync(new SongNameLanguageEntity
        {
            CultureCode = "ko-KR",
            EnglishSongName = name.ToString(),
            SongName = koreanName.ToString()
        }, cancellationToken);
        return newSong.Id;
    }

    public async Task SetChartVideo(Guid id, Uri videoUrl, Name channelName,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var entity = await database.ChartVideo.FirstOrDefaultAsync(c => c.ChartId == id, cancellationToken);
        if (entity == null)
        {
            await database.ChartVideo.AddAsync(new ChartVideoEntity
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

    public async Task UpdateNoteCount(Guid chartId, int noteCount, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixGuids[MixEnum.Phoenix];
        var entity =
            await database.ChartMix.FirstOrDefaultAsync(c => c.ChartId == chartId && c.MixId == mixId,
                cancellationToken);
        if (entity == null) return;
        entity.NoteCount = noteCount;
        await database.SaveChangesAsync(cancellationToken);

        var cache = await GetAllCharts(MixEnum.Phoenix, cancellationToken);
        var chart = cache[chartId];
        cache[chartId] = chart with { Id = chartId, NoteCount = noteCount };
    }


    private const string ChartSkillsCacheKey = $"{nameof(EFChartRepository)}_{nameof(GetChartSkills)}";

    private sealed record ChartSkillJoin(Guid ChartId, string SkillName);

    public async Task<IEnumerable<ChartSkillsRecord>> GetChartSkills(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync(ChartSkillsCacheKey, async o =>
        {
            o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(1);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            return (await database.ChartSkill.ToArrayAsync(cancellationToken))
                .GroupBy(c => c.ChartId)
                .Select(g => new ChartSkillsRecord(g.Key, g.Select(s => Enum.Parse<Skill>(s.SkillName)),
                    g.Where(s => s.IsHighlighted).Select(s => Enum.Parse<Skill>(s.SkillName))))
                .ToArray();
        });
    }

    public async Task SaveChartSkills(ChartSkillsRecord record, CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var skills = await database.ChartSkill.Where(cs => cs.ChartId == record.ChartId)
            .ToArrayAsync(cancellationToken);

        var newEntities = record.ContainsSkills.Select(s => new ChartSkillEntity
        {
            ChartId = record.ChartId,
            Id = Guid.NewGuid(),
            IsHighlighted = false,
            SkillName = s.ToString()
        }).Concat(record.HighlightsSkill.Select(s => new ChartSkillEntity
        {
            ChartId = record.ChartId,
            Id = Guid.NewGuid(),
            IsHighlighted = true,
            SkillName = s.ToString()
        })).ToArray();

        var toCreate = newEntities.Where(e =>
            !skills.Any(s => s.SkillName == e.SkillName && s.IsHighlighted == e.IsHighlighted));

        var toDelete = skills.Where(e =>
            !newEntities.Any(s => s.SkillName == e.SkillName && s.IsHighlighted == e.IsHighlighted));
        await database.ChartSkill.AddRangeAsync(toCreate, cancellationToken);
        database.ChartSkill.RemoveRange(toDelete);
        await database.SaveChangesAsync(cancellationToken);

        var cache = await GetAllCharts(MixEnum.Phoenix, cancellationToken);
        var chart = cache[record.ChartId];
        cache[record.ChartId] = chart with { Skills = record.ContainsSkills.Distinct().ToHashSet() };
        _cache.Remove(ChartSkillsCacheKey);
    }

    public async Task SetSongCultureName(Name englishSongName, Name cultureCode, Name songName,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var englishString = englishSongName.ToString();
        var cultureString = cultureCode.ToString();
        var entity = await database.SongNameLanguage.FirstOrDefaultAsync(
            n => n.CultureCode == cultureString && n.EnglishSongName == englishString, cancellationToken);
        if (entity == null)
            await database.SongNameLanguage.AddAsync(new SongNameLanguageEntity
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
            return (await database.SongNameLanguage.Where(s => s.CultureCode == cultureString)
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
        foreach (var mixId in MixGuids.Values)
        {
            var key = ChartCacheKey(mixId);
            _cache.Remove(key);
        }

        _cache.Remove(ChartSkillsCacheKey);
        _cache.Remove($"{nameof(EFChartRepository)}_{nameof(GetChartVideoInformation)}");
    }

    public async Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl, Name stepArtist,
        CancellationToken cancellationToken = default)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var newChart = new ChartEntity
        {
            DifficultyRating = null,
            Id = Guid.NewGuid(),
            Level = level,
            SongId = songId,
            Type = type.ToString(),
            StepArtist = stepArtist
        };
        var newChartMix = new ChartMixEntity
        {
            ChartId = newChart.Id,
            Id = Guid.NewGuid(),
            Level = level,
            MixId = MixGuids[mix]
        };
        var newChartVideo = new ChartVideoEntity
        {
            ChartId = newChart.Id,
            ChannelName = channelName,
            VideoUrl = videoUrl.ToString()
        };
        await database.Chart.AddAsync(newChart, cancellationToken);
        await database.ChartMix.AddAsync(newChartMix, cancellationToken);
        await database.ChartVideo.AddAsync(newChartVideo, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
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

    private static string ChartCacheKey(Guid mixId)
    {
        return $"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}_Mix:{mixId}";
    }

    private async Task<IDictionary<Guid, Chart>> GetAllCharts(MixEnum mix, CancellationToken cancellationToken)
    {
        var mixId = MixGuids[mix];
        return await _cache.GetOrCreateAsync<IDictionary<Guid, Chart>>(ChartCacheKey(mixId), async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var chartSkills = (await database.ChartSkill.ToArrayAsync(cancellationToken)).GroupBy(cs => cs.ChartId)
                .ToDictionary(g => g.Key, g => g.Select(s => Enum.Parse<Skill>(s.SkillName)).Distinct().ToHashSet());

            return await (from cm in database.ChartMix
                    join c in database.Chart on cm.ChartId equals c.Id
                    join s in database.Song on c.SongId equals s.Id
                    join m in database.Mix on c.OriginalMixId equals m.Id
                    where cm.MixId == mixId
                    select new Chart(c.Id, Enum.Parse<MixEnum>(m.Name),
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration,
                            s.Artist ?? "Unknown",
                            Bpm.From(s.MinBpm, s.MaxBpm)),
                        Enum.Parse<ChartType>(c.Type),
                        cm.Level, mix, c.StepArtist, cm.ScoringLevel, cm.NoteCount,
                        new HashSet<Skill>()))
                .ToDictionaryAsync(c => c.Id,
                    c => c with
                    {
                        Skills = chartSkills.TryGetValue(c.Id, out var skill) ? skill : new HashSet<Skill>()
                    }, cancellationToken);
        });
    }
}
