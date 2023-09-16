using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
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
    private readonly ChartAttemptDbContext _database;

    public EFChartRepository(ChartAttemptDbContext database, IMemoryCache cache)
    {
        _database = database;
        _cache = cache;
    }

    public async Task<IEnumerable<Name>> GetSongNames(MixEnum mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        return (await (from cm in _database.ChartMix
            join c in _database.Chart on cm.ChartId equals c.Id
            join s in _database.Song on c.SongId equals s.Id
            where cm.MixId == mixId
            select s.Name).Distinct().ToArrayAsync(cancellationToken)).Select(Name.From);
    }

    public async Task<Chart> GetChart(MixEnum mix, Guid chartId, CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var charts = await GetAllCharts(mixId, cancellationToken);
        return charts[chartId];
    }


    public async Task<IEnumerable<Chart>> GetChartsForSong(MixEnum mix, Name songName,
        CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var charts = await GetAllCharts(mixId, cancellationToken);
        return charts.Values.Where(c => c.Song.Name == songName);
    }


    public async Task<IEnumerable<Chart>> GetCoOpCharts(MixEnum mix, CancellationToken cancellationToken = default)
    {
        var mixId = MixGuids[mix];
        var charts = await GetAllCharts(mixId, cancellationToken);
        return charts.Values.Where(c => c.Type == ChartType.CoOp);
    }

    public async Task<IEnumerable<ChartVideoInformation>> GetChartVideoInformation(
        IEnumerable<Guid>? chartIds = default, CancellationToken cancellationToken = default)
    {
        const string key = $"{nameof(EFChartRepository)}_{nameof(GetChartVideoInformation)}";
        var chartVideos = await _cache.GetOrCreateAsync<IDictionary<Guid, ChartVideoInformation>>(key,
            async entry =>
            {
                entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
                return await _database.ChartVideo
                    .Select(c => new ChartVideoInformation(c.ChartId, new Uri(c.VideoUrl), c.ChannelName))
                    .ToDictionaryAsync(c => c.ChartId, cancellationToken);
            });

        return chartIds != null ? chartIds.Select(id => chartVideos[id]) : chartVideos.Values;
    }

    public async Task<Guid> CreateSong(Name name, Uri imageUrl, SongType type, TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var newSong = new SongEntity
        {
            Id = Guid.NewGuid(),
            ImagePath = imageUrl.ToString(),
            Name = name,
            Type = type.ToString(),
            Duration = duration
        };
        await _database.Song.AddAsync(newSong, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);

        return newSong.Id;
    }

    public async Task SetSongDuration(Name songName, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var nameString = songName.ToString();
        var song = await _database.Song.SingleAsync(s => s.Name == nameString, cancellationToken);
        song.Duration = duration;
        await _database.SaveChangesAsync(cancellationToken);
        ClearCache();
    }

    public void ClearCache()
    {
        foreach (var mixId in MixGuids.Values)
        {
            var key = $"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}_Mix:{mixId}";
            _cache.Remove(key);
        }

        _cache.Remove($"{nameof(EFChartRepository)}_{nameof(GetChartVideoInformation)}");
    }

    public async Task<Guid> CreateChart(MixEnum mix, Guid songId, ChartType type, DifficultyLevel level,
        Name channelName, Uri videoUrl,
        CancellationToken cancellationToken = default)
    {
        var newChart = new ChartEntity
        {
            DifficultyRating = null,
            Id = Guid.NewGuid(),
            Level = level,
            SongId = songId,
            Type = type.ToString()
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
        await _database.Chart.AddAsync(newChart, cancellationToken);
        await _database.ChartMix.AddAsync(newChartMix, cancellationToken);
        await _database.ChartVideo.AddAsync(newChartVideo, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
        return newChart.Id;
    }

    public async Task UpgradeSong(Name songName, CancellationToken cancellationToken = default)
    {
        var phoenixId = MixGuids[MixEnum.Phoenix];
        var xxId = MixGuids[MixEnum.XX];
        var songString = songName.ToString();
        var xxEntry = await (from s in _database.Song
                join c in _database.Chart on s.Id equals c.SongId
                join cm in _database.ChartMix on c.Id equals cm.ChartId
                where s.Name == songString && cm.MixId == xxId
                select cm)
            .ToArrayAsync(cancellationToken);

        var phoenixEntries = xxEntry.Select(e => new ChartMixEntity
        {
            ChartId = e.ChartId,
            Id = Guid.NewGuid(),
            Level = e.Level,
            MixId = phoenixId
        }).ToArray();
        await _database.ChartMix.AddRangeAsync(phoenixEntries, cancellationToken);
        await _database.SaveChangesAsync(cancellationToken);
        _cache.Remove($"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}_Mix:{phoenixId}");
    }

    public async Task<IEnumerable<Chart>> GetCharts(MixEnum mix, DifficultyLevel? level = null,
        ChartType? type = null,
        IEnumerable<Guid>? chartIds = null,
        CancellationToken cancellationToken = default)
    {
        var result =
            (await GetAllCharts(MixGuids[mix], cancellationToken)).Values.AsEnumerable();
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

    private async Task<IDictionary<Guid, Chart>> GetAllCharts(Guid mixId, CancellationToken cancellationToken)
    {
        var key = $"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}_Mix:{mixId}";
        return await _cache.GetOrCreateAsync<IDictionary<Guid, Chart>>(key, async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            return await (from cm in _database.ChartMix
                    join c in _database.Chart on cm.ChartId equals c.Id
                    join s in _database.Song on c.SongId equals s.Id
                    where cm.MixId == mixId
                    select new Chart(c.Id,
                        new Song(s.Name, Enum.Parse<SongType>(s.Type), new Uri(s.ImagePath), s.Duration),
                        Enum.Parse<ChartType>(c.Type),
                        cm.Level))
                .ToDictionaryAsync(c => c.Id, c => c, cancellationToken);
        });
    }
}