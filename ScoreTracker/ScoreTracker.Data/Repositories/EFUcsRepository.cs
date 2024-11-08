using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFUcsRepository : IUcsRepository
    {
        private readonly IDbContextFactory<ChartAttemptDbContext> _dbFactory;

        public EFUcsRepository(IDbContextFactory<ChartAttemptDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        public async Task<IEnumerable<UcsChart>> GetUcsCharts(CancellationToken cancellationToken)
        {
            var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var ucsEntities = await database.UcsChart.ToArrayAsync(cancellationToken);
            var songIds = ucsEntities.Select(u => u.SongId).Distinct().ToArray();
            var songs = (await database.Song.Where(s => songIds.Contains(s.Id)).ToArrayAsync(cancellationToken))
                .ToDictionary(s => s.Id, s => new Song(s.Name, Enum.Parse<SongType>(s.Type),
                    new Uri(s.ImagePath, UriKind.Absolute), s.Duration, s.Artist,
                    Bpm.From(s.MinBpm, s.MaxBpm)));
            var counts = (await database.UcsChartLeaderboardEntry.ToArrayAsync(cancellationToken))
                .GroupBy(e => e.ChartId)
                .ToDictionary(g => g.Key, g => g.Count());

            return ucsEntities.Select(e => new UcsChart(e.PiuGameId, new Chart(
                    e.Id, songs[e.SongId], Enum.Parse<ChartType>(e.ChartType), e.Level, MixEnum.Phoenix, e.Artist,
                    e.Level, null, new HashSet<Skill>()),
                e.Uploader, e.Artist, e.Description, counts.TryGetValue(e.Id, out var c) ? c : 0));
        }

        public async Task CreateUcsChart(UcsChart chart, CancellationToken cancellationToken)
        {
            var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var songString = chart.Chart.Song.Name.ToString();
            var songId = (await database.Song.Where(s => s.Name == songString).FirstAsync(cancellationToken)).Id;

            var entity = await database.UcsChart.Where(u => u.Id == chart.Chart.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.UcsChart.AddAsync(new UcsChartEntity
                {
                    Artist = chart.Artist,
                    ChartType = chart.Chart.Type.ToString(),
                    Description = chart.Description,
                    Id = chart.Chart.Id,
                    Level = chart.Chart.Level,
                    PiuGameId = chart.PiuGameId,
                    SongId = songId,
                    Uploader = chart.Uploader
                }, cancellationToken);
                await database.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<UcsLeaderboardEntry>> GetChartLeaderboard(Guid chartId,
            CancellationToken cancellationToken)
        {
            var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
            return (await database.UcsChartLeaderboardEntry.Where(e => e.ChartId == chartId)
                    .ToArrayAsync(cancellationToken))
                .Select(e => new UcsLeaderboardEntry(e.UserId, e.Score, Enum.Parse<PhoenixPlate>(e.Plate), e.IsBroken,
                    e.VideoPath == null ? null : new Uri(e.VideoPath, UriKind.Absolute),
                    e.ImageUrl == null ? null : new Uri(e.ImageUrl, UriKind.Absolute))).ToArray();
        }

        public async Task UpdateScore(Guid chartId, Guid userId, PhoenixScore score, PhoenixPlate plate, bool isBroken,
            Uri? videoPath,
            Uri? imagePath, CancellationToken cancellationToken)
        {
            var database = await _dbFactory.CreateDbContextAsync(cancellationToken);
            var entity = await database.UcsChartLeaderboardEntry.Where(e => e.ChartId == chartId && e.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.UcsChartLeaderboardEntry.AddAsync(new UcsChartLeaderboardEntryEntity
                {
                    ChartId = chartId,
                    UserId = userId,
                    ImageUrl = imagePath?.ToString(),
                    IsBroken = isBroken,
                    Plate = plate.ToString(),
                    RecordedOn = DateTimeOffset.Now,
                    Score = score,
                    VideoPath = videoPath?.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.Score = score;
                entity.ImageUrl = imagePath?.ToString();
                entity.IsBroken = isBroken;
                entity.VideoPath = videoPath?.ToString();
                entity.RecordedOn = DateTimeOffset.Now;
                entity.Plate = plate.ToString();
            }

            await database.SaveChangesAsync(cancellationToken);
        }
    }
}
