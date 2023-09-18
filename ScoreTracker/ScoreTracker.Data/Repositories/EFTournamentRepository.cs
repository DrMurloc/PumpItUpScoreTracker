using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFTournamentRepository : ITournamentRepository
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ChartAttemptDbContext _database;
        private readonly IChartRepository _charts;

        public EFTournamentRepository(IMemoryCache memoryCache, IChartRepository charts, ChartAttemptDbContext database)
        {
            _memoryCache = memoryCache;
            _database = database;
            _charts = charts;
        }

        private static string TourneyCacheKey = $@"{nameof(EFTournamentRepository)}_Tournies";
        private static string TourneyIdCacheKey(Guid id) => $@"{nameof(EFTournamentRepository)}_Tourney_{id}";

        private sealed record TouramentParticpantCount(Guid TournamentId, int count)
        {
        }

        public async Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyCacheKey, async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
                var counts = (await _database.UserTournamentSession.ToArrayAsync(cancellationToken))
                    .GroupBy(uts => uts.TournamentId)
                    .ToDictionary(g => g.Key, g => g.Count());

                return (await _database.Tournament.ToArrayAsync(cancellationToken)).Select(t =>
                    new TournamentRecord(t.Id, t.Name, counts.TryGetValue(t.Id, out var count) ? count : 0, t.StartDate,
                        t.EndDate)).ToArray();
            });
        }

        public async Task<TournamentConfiguration> GetTournament(Guid id, CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyIdCacheKey(id), async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
                var result = await _database.Tournament.Where(t => t.Id == id).SingleAsync(cancellationToken);
                return JsonSerializer.Deserialize<TournamentConfigurationJsonEntity>(result.Configuration)?.To() ??
                       throw new Exception($"Tournament {id} was not configured properly");
            });
        }

        public async Task CreateOrSaveTournament(TournamentConfiguration tournament,
            CancellationToken cancellationToken)
        {
            var entity = await _database.Tournament.FirstOrDefaultAsync(t => t.Id == tournament.Id, cancellationToken);
            if (entity == null)
            {
                await _database.Tournament.AddAsync(new TournamentEntity
                {
                    Id = tournament.Id,
                    Configuration = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(tournament)),
                    EndDate = tournament.EndDate,
                    StartDate = tournament.StartDate,
                    Name = tournament.Name
                }, cancellationToken);
            }
            else
            {
                entity.Name = tournament.Name;
                entity.EndDate = tournament.EndDate;
                entity.StartDate = tournament.StartDate;
                entity.Configuration = JsonSerializer.Serialize(TournamentConfigurationJsonEntity.From(tournament));
            }

            await _database.SaveChangesAsync(cancellationToken);
            _memoryCache.Remove(TourneyCacheKey);
            _memoryCache.Remove(TourneyIdCacheKey(tournament.Id));
        }

        public async Task SaveSession(TournamentSession session, CancellationToken cancellationToken)
        {
            _memoryCache.Remove(TourneyCacheKey);
            var entity = await _database.UserTournamentSession.FirstOrDefaultAsync(
                uts => uts.TournamentId == session.TournamentId && uts.UserId == session.UsersId, cancellationToken);
            if (entity == null)
            {
                await _database.UserTournamentSession.AddAsync(new UserTournamentSessionEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = session.UsersId,
                    TournamentId = session.TournamentId,
                    SessionScore = session.TotalScore,
                    RestTime = session.CurrentRestTime,
                    ChartsPlayed = session.Entries.Count(),
                    AverageDifficulty = session.Entries.Average(e => e.Chart.Level),
                    ChartEntries = JsonSerializer.Serialize(session.Entries.Select(e => new SessionEntryEntity
                    {
                        ChartId = e.Chart.Id,
                        IsBroken = e.IsBroken,
                        Plate = e.Plate.ToString(),
                        Score = e.Score,
                        SessionScore = e.SessionScore
                    }))
                }, cancellationToken);
            }
            else
            {
                entity.SessionScore = session.TotalScore;
                entity.RestTime = session.CurrentRestTime;
                entity.ChartsPlayed = session.Entries.Count();
                entity.AverageDifficulty = session.Entries.Average(e => e.Chart.Level);
                entity.ChartEntries = JsonSerializer.Serialize(session.Entries.Select(e => new SessionEntryEntity
                {
                    ChartId = e.Chart.Id,
                    IsBroken = e.IsBroken,
                    Plate = e.Plate.ToString(),
                    Score = e.Score,
                    SessionScore = e.SessionScore
                }));
            }

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task<TournamentSession> GetSession(Guid tournamentId, Guid userId,
            CancellationToken cancellationToken)
        {
            var entity = await _database.UserTournamentSession.FirstOrDefaultAsync(
                uts => uts.TournamentId == tournamentId && uts.UserId == userId, cancellationToken);
            var tournamentConfig = await GetTournament(tournamentId, cancellationToken);
            if (entity == null)
            {
                return new TournamentSession(userId, tournamentConfig);
            }

            var entryEntities = JsonSerializer.Deserialize<SessionEntryEntity[]>(entity.ChartEntries) ??
                                Array.Empty<SessionEntryEntity>();
            var charts = (await _charts.GetCharts(MixEnum.Phoenix,
                chartIds: entryEntities.Select(e => e.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken)).ToDictionary(c => c.Id);
            var entries = entryEntities.Select(e => new TournamentSession.Entry(charts[e.ChartId], e.Score,
                Enum.Parse<PhoenixPlate>(e.Plate), e.IsBroken, e.SessionScore));
            return new TournamentSession(userId, tournamentConfig, entries);
        }

        public async Task<IEnumerable<LeaderboardRecord>> GetLeaderboardRecords(Guid tournamentId,
            CancellationToken cancellationToken)
        {
            return (await (from uts in _database.UserTournamentSession
                    join u in _database.User on uts.UserId equals u.Id
                    where uts.TournamentId == tournamentId
                    select new UserEntryDto(u.Id, u.Name, uts.SessionScore, uts.RestTime, uts.ChartsPlayed,
                        uts.AverageDifficulty)).ToArrayAsync(cancellationToken))
                .OrderByDescending(ue => ue.Score)
                .Select((ue, index) => new LeaderboardRecord(index + 1, ue.UserId, ue.Name, ue.Score, ue.RestTime,
                    ue.AverageDifficulty, ue.ChartsPlayed));
        }

        private sealed record UserEntryDto(Guid UserId, string Name, int Score, TimeSpan RestTime, int ChartsPlayed,
            double AverageDifficulty)
        {
        }
    }
}