using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories;

public sealed class EFChartRepository : IChartRepository
{
    private readonly ChartAttemptDbContext _database;

    public EFChartRepository(ChartAttemptDbContext database)
    {
        _database = database;
    }

    public async Task<IEnumerable<Name>> GetSongNames(CancellationToken cancellationToken = default)
    {
        return (await _database.Song.Select(s => s.Name).ToArrayAsync(cancellationToken)).Select(Name.From);
    }

    public async Task<Chart> GetChart(Name songName, ChartType chartType, DifficultyLevel level,
        CancellationToken cancellationToken = default)
    {
        return await (from s in _database.Song
            join c in _database.Chart on s.Id equals c.SongId
            where s.Name == (string)songName && c.Type == chartType.ToString() && c.Level == (int)level
            select new Chart(s.Name, Enum.Parse<ChartType>(c.Type), c.Level)).SingleAsync(cancellationToken);
    }

    public async Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default)
    {
        var nameString = (string)songName;
        return await (from s in _database.Song
            join c in _database.Chart on s.Id equals c.SongId
            where s.Name == nameString
            select new Chart(s.Name, Enum.Parse<ChartType>(c.Type), c.Level)).ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<Chart>> GetChartsByDifficulty(DifficultyLevel difficultyLevel,
        CancellationToken cancellationToken = default)
    {
        var difficultyInt = (int)difficultyLevel;
        return await (from c in _database.Chart
            join s in _database.Song on c.SongId equals s.Id
            where c.Level == difficultyInt && c.Type != ChartType.CoOp.ToString()
            select new Chart(s.Name, Enum.Parse<ChartType>(c.Type), c.Level)).ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<Chart>> GetCoOpCharts(CancellationToken cancellationToken = default)
    {
        return await (from c in _database.Chart
            join s in _database.Song on c.SongId equals s.Id
            where c.Type == ChartType.CoOp.ToString()
            select new Chart(s.Name, Enum.Parse<ChartType>(c.Type), c.Level)).ToArrayAsync(cancellationToken);
    }
}