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
    }
}
