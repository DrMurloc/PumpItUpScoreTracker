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

    public async Task<IEnumerable<Chart>> GetChartsForSong(Name songName, CancellationToken cancellationToken = default)
    {
        var nameString = (string)songName;
        return await (from s in _database.Song
            join c in _database.Chart on s.Id equals c.SongId
            where s.Name == nameString
            select new Chart(s.Name, Enum.Parse<ChartType>(c.Type), c.Level)).ToArrayAsync(cancellationToken);
    }
}