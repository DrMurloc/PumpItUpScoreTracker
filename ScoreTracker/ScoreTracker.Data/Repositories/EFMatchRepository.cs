using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFMatchRepository : IMatchRepository
    {
        private readonly ChartAttemptDbContext _dbContext;
        private readonly JsonSerializerOptions _jsonOptions;

        public EFMatchRepository(IDbContextFactory<ChartAttemptDbContext> factory,
            IOptions<JsonSerializerOptions> jsonOptions)
        {
            _dbContext = factory.CreateDbContext();
            _jsonOptions = jsonOptions.Value;
        }

        public async Task<MatchView> GetMatch(Name matchName, CancellationToken cancellationToken)
        {
            var nameString = matchName.ToString();
            var entity = await _dbContext.Match.Where(m => m.Name == nameString).FirstAsync(cancellationToken);
            return JsonSerializer.Deserialize<MatchView>(entity.Json, _jsonOptions) ??
                   throw new JsonException($"Couldn't parse json for match {matchName} {entity.Id}");
        }

        public async Task<IEnumerable<MatchView>> GetAllMatches(CancellationToken cancellationToken)
        {
            var entities = await _dbContext.Match.ToArrayAsync(cancellationToken);
            return entities.Select(e =>
                JsonSerializer.Deserialize<MatchView>(e.Json, _jsonOptions) ??
                throw new JsonException($"Couldn't parse json for match {e.Name} {e.Id}"));
        }

        public async Task SaveMatch(MatchView matchView, CancellationToken cancellationToken)
        {
            var nameString = matchView.MatchName.ToString();
            var entity = await _dbContext.Match.Where(m => m.Name == nameString).FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
                await _dbContext.Match.AddAsync(new MatchEntity
                {
                    Id = Guid.NewGuid(),
                    Name = nameString,
                    Json = JsonSerializer.Serialize(matchView, _jsonOptions)
                }, cancellationToken);
            else
                entity.Json = JsonSerializer.Serialize(matchView, _jsonOptions);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveRandomSettings(Name settingsName, RandomSettings settings,
            CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entity = await _dbContext.RandomSettings.Where(m => m.Name == nameString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
                await _dbContext.RandomSettings.AddAsync(new RandomSettingsEntity
                {
                    Id = Guid.NewGuid(),
                    Name = nameString,
                    Json = JsonSerializer.Serialize(settings, _jsonOptions)
                }, cancellationToken);
            else
                entity.Json = JsonSerializer.Serialize(settings, _jsonOptions);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<RandomSettings> GetRandomSettings(Name settingsName, CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entity = await _dbContext.RandomSettings.Where(r => r.Name == nameString).FirstAsync(cancellationToken);
            return JsonSerializer.Deserialize<RandomSettings>(entity.Json, _jsonOptions) ??
                   throw new JsonException($"Couldn't deserialize random settings {entity.Name} {entity.Id}");
        }

        public async Task<IEnumerable<(Name name, RandomSettings settings)>> GetAllRandomSettings(
            CancellationToken cancellationToken)
        {
            var entities = await _dbContext.RandomSettings.ToArrayAsync(cancellationToken);
            return entities.Select(e => (Name.From(e.Name),
                    JsonSerializer.Deserialize<RandomSettings>(e.Json, _jsonOptions) ??
                    throw new JsonException($"Error deserializing random settings {e.Name} {e.Id}")))
                .ToArray();
        }

        public async Task<IEnumerable<MatchLink>> GetMatchLinksByFromMatchName(Name fromMatchName,
            CancellationToken cancellationToken)
        {
            var nameString = fromMatchName.ToString();
            return await _dbContext.MatchLink.Where(m => m.FromMatch == nameString)
                .Select(e => new MatchLink(e.FromMatch, e.ToMatch, e.IsWinners, e.PlayerCount))
                .ToArrayAsync(cancellationToken);
        }

        public async Task SaveMatchLink(MatchLink matchLink, CancellationToken cancellationToken)
        {
            var fromString = matchLink.FromMatch.ToString();
            var toString = matchLink.ToMatch.ToString();
            var entity = await _dbContext.MatchLink.Where(m => m.FromMatch == fromString && m.ToMatch == toString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await _dbContext.MatchLink.AddAsync(new MatchLinkEntity
                {
                    Id = Guid.NewGuid(),
                    FromMatch = fromString,
                    ToMatch = toString,
                    IsWinners = matchLink.IsWinners,
                    PlayerCount = matchLink.PlayerCount
                }, cancellationToken);
            }
            else
            {
                entity.IsWinners = matchLink.IsWinners;
                entity.PlayerCount = matchLink.PlayerCount;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteMatchLink(Name fromName, Name toName, CancellationToken cancellationToken)
        {
            var fromString = fromName.ToString();
            var toString = toName.ToString();
            var entity = await _dbContext.MatchLink.Where(m => m.FromMatch == fromString && m.ToMatch == toString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity != null)
            {
                _dbContext.MatchLink.Remove(entity);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<IEnumerable<MatchLink>> GetAllMatchLinks(CancellationToken cancellationToken)
        {
            return await _dbContext.MatchLink
                .Select(ml => new MatchLink(ml.FromMatch, ml.ToMatch, ml.IsWinners, ml.PlayerCount))
                .ToArrayAsync(cancellationToken);
        }

        public Task<IEnumerable<MatchPlayer>> GetMatchPlayers(CancellationToken cancellationToken)
        {
            return Task.FromResult(PlayerOrders);
        }

        public static readonly IEnumerable<MatchPlayer> PlayerOrders = new MatchPlayer[]
        {
            new("QED", 56, 477504512207093841),
            new("Snowstorm", 55, 477504512207093841),
            new("Tommy Doesn't Miss", 54, 477504512207093841),
            new("Sneezle", 53, 477504512207093841),
            new("Kwuarter", 52, 477504512207093841),
            new("PrimoVictorian", 51, 477504512207093841),
            new("Ulsi", 50, 477504512207093841),
            new("Frac", 49, 477504512207093841),
            new("Houseplant", 48, 477504512207093841),
            new("Nyroom", 47, 477504512207093841),
            new("DefaultK", 46, 477504512207093841),
            new("EMCAT", 45, 477504512207093841),
            new("Smallboy", 44, 477504512207093841),
            new("Slowpoke", 43, 477504512207093841),
            new("ancient_grainz", 42, 477504512207093841),
            new("PacRob", 41, 477504512207093841),
            new("Crafty The Fox", 40, 477504512207093841),
            new("NESSQUICK", 39, 477504512207093841),
            new("Songbird", 38, 477504512207093841),
            new("StrawHatGabe", 37, 477504512207093841),
            new("Tink", 36, 477504512207093841),
            new("Shinobee", 35, 477504512207093841),
            new("ligma", 34, 477504512207093841),
            new("Surikato", 33, 477504512207093841),
            new("SEBAA", 32, 477504512207093841),
            new("Waffle", 31, 477504512207093841),
            new("litenang", 30, 477504512207093841),
            new("Bedrock", 29, 477504512207093841),
            new("jonathan", 28, 477504512207093841),
            new("HSPuppets", 27, 477504512207093841),
            new("ABENHAIM", 26, 477504512207093841),
            new("s0 lost", 25, 477504512207093841),
            new("Lulu_uwu", 24, 477504512207093841),
            new("sixxofsixx", 23, 477504512207093841),
            new("Chives", 22, 477504512207093841),
            new("Valex", 21, 477504512207093841),
            new("Flashy flash", 20, 477504512207093841),
            new("Ermagerd", 19, 477504512207093841),
            new("Tieny", 18, 477504512207093841),
            new("Blankman", 17, 477504512207093841),
            new("ZIGGURATH8", 16, 477504512207093841),
            new("Jaekim", 15, 477504512207093841),
            new("esi", 14, 477504512207093841),
            new("Yimmythe42", 13, 477504512207093841),
            new("PureWasian", 12, 477504512207093841),
            new("Redviper", 11, 477504512207093841),
            new("imDrake", 10, 477504512207093841),
            new("comboscoring", 9, 477504512207093841),
            new("JellySlosh", 8, 477504512207093841),
            new("GODDISH", 7, 477504512207093841),
            new("AwesomoBird", 6, 477504512207093841),
            new("Jboy", 5, 477504512207093841),
            new("jqtran", 4, 477504512207093841),
            new("ParanoiaBoi", 3, 477504512207093841),
            new("HDS", 2, 477504512207093841),
            new("mattmiller", 1, 477504512207093841)
        };
    }
}
