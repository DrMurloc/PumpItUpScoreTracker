using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartRepository : IChartRepository
{
    private readonly IMemoryCache _cache;
    private readonly ChartAttemptDbContext _database;

    public EFChartRepository(ChartAttemptDbContext database, IMemoryCache cache)
    {
        _database = database;
        _cache = cache;
    }

    public async Task<IEnumerable<Chart>> GetCharts(DifficultyLevel? level = null, ChartType? type = null,
        CancellationToken cancellationToken = default)
    {
        var result = (await GetAllCharts(cancellationToken)).Values.AsEnumerable();
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

    public async Task<IEnumerable<Name>> GetSongNames(CancellationToken cancellationToken = default)
    {
        return (await _database.Song.Select(s => s.Name).ToArrayAsync(cancellationToken)).Select(Name.From);
    }

    public async Task<Chart> GetChart(Guid chartId, CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(cancellationToken);
        return charts[chartId];
    }


    public async Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(cancellationToken);
        return charts.Values.Where(c => c.Song.Name == songName);
    }


    public async Task<IEnumerable<Chart>> GetCoOpCharts(CancellationToken cancellationToken = default)
    {
        var charts = await GetAllCharts(cancellationToken);
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
                return _database.ChartVideo
                    .Select(c => new ChartVideoInformation(c.ChartId, new Uri(c.VideoUrl), c.ChannelName))
                    .ToDictionary(c => c.ChartId);
            });

        return chartIds != null ? chartIds.Select(id => chartVideos[id]) : chartVideos.Values;
    }

    private async Task<IDictionary<Guid, Chart>> GetAllCharts(CancellationToken cancellationToken)
    {
        const string key = $"{nameof(EFChartRepository)}_{nameof(GetAllCharts)}";
        return await _cache.GetOrCreateAsync<IDictionary<Guid, Chart>>(key, async entry =>
        {
            entry.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromDays(14);
            return await (from c in _database.Chart
                    join s in _database.Song on c.SongId equals s.Id
                    select new Chart(c.Id, new Song(s.Name, new Uri(s.ImagePath)), Enum.Parse<ChartType>(c.Type),
                        c.Level))
                .ToDictionaryAsync(c => c.Id, c => c, cancellationToken);
        });
    }
}