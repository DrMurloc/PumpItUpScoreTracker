using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFTournamentRepository : ITournamentRepository
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ChartAttemptDbContext _database;

        public EFTournamentRepository(IMemoryCache memoryCache, ChartAttemptDbContext database)
        {
            _memoryCache = memoryCache;
            _database = database;
        }

        private static string TourneyCacheKey = $@"{nameof(EFTournamentRepository)}_Tournies";
        private static string TourneyIdCacheKey(Guid id) => $@"{nameof(EFTournamentRepository)}_Tourney_{id}";

        public async Task<IEnumerable<TournamentRecord>> GetAllTournaments(CancellationToken cancellationToken)
        {
            return await _memoryCache.GetOrCreateAsync(TourneyCacheKey, async o =>
            {
                o.AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromMinutes(60);
                return await _database.Tournament.Select(t =>
                    new TournamentRecord(t.Id, t.Name, 0, t.StartDate, t.EndDate)).ToArrayAsync(cancellationToken);
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
    }
}