using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Data.Repositories;

public sealed class EFMatchRepository : IMatchRepository
{
    private readonly ChartAttemptDbContext _dbContext;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly IMemoryCache _cache;

    public EFMatchRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IOptions<JsonSerializerOptions> jsonOptions,
        IMemoryCache cache)
    {
        _dbContext = factory.CreateDbContext();
        _jsonOptions = jsonOptions.Value;
        _cache = cache;
    }

    public async Task<MatchView> GetMatch(Guid tournamentId, Name matchName, CancellationToken cancellationToken)
    {
        var nameString = matchName.ToString();
        var entity = await _dbContext.Match.Where(m => m.TournamentId == tournamentId && m.Name == nameString)
            .FirstAsync(cancellationToken);
        return JsonSerializer.Deserialize<MatchView>(entity.Json, _jsonOptions) ??
               throw new JsonException($"Couldn't parse json for match {matchName} {entity.Id}");
    }

    public async Task<IEnumerable<MatchView>> GetAllMatches(Guid tournamentId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.Match.Where(m => m.TournamentId == tournamentId)
            .ToArrayAsync(cancellationToken);
        return entities.Select(e =>
            JsonSerializer.Deserialize<MatchView>(e.Json, _jsonOptions) ??
            throw new JsonException($"Couldn't parse json for match {e.Name} {e.Id}"));
    }

    public async Task SaveMatch(Guid tournamentId, MatchView matchView, CancellationToken cancellationToken)
    {
        var nameString = matchView.MatchName.ToString();
        var entity = await _dbContext.Match.Where(m => m.TournamentId == tournamentId && m.Name == nameString)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
            await _dbContext.Match.AddAsync(new MatchEntity
            {
                TournamentId = tournamentId,
                Id = Guid.NewGuid(),
                Name = nameString,
                Json = JsonSerializer.Serialize(matchView, _jsonOptions)
            }, cancellationToken);
        else
            entity.Json = JsonSerializer.Serialize(matchView, _jsonOptions);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveRandomSettings(Guid tournamentId, Name settingsName, RandomSettings settings,
        CancellationToken cancellationToken)
    {
        var nameString = settingsName.ToString();
        var entity = await _dbContext.RandomSettings
            .Where(m => m.TournamentId == tournamentId && m.Name == nameString)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
            await _dbContext.RandomSettings.AddAsync(new RandomSettingsEntity
            {
                TournamentId = tournamentId,
                Id = Guid.NewGuid(),
                Name = nameString,
                Json = JsonSerializer.Serialize(settings, _jsonOptions)
            }, cancellationToken);
        else
            entity.Json = JsonSerializer.Serialize(settings, _jsonOptions);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RandomSettings> GetRandomSettings(Guid tournamentId, Name settingsName,
        CancellationToken cancellationToken)
    {
        var nameString = settingsName.ToString();
        var entity = await _dbContext.RandomSettings
            .Where(r => r.TournamentId == tournamentId && r.Name == nameString).FirstAsync(cancellationToken);
        return JsonSerializer.Deserialize<RandomSettings>(entity.Json, _jsonOptions) ??
               throw new JsonException($"Couldn't deserialize random settings {entity.Name} {entity.Id}");
    }

    public async Task<IEnumerable<(Name name, RandomSettings settings)>> GetAllRandomSettings(Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var entities = await _dbContext.RandomSettings.ToArrayAsync(cancellationToken);
        return entities.Where(t => t.TournamentId == tournamentId).Select(e => (Name.From(e.Name),
                JsonSerializer.Deserialize<RandomSettings>(e.Json, _jsonOptions) ??
                throw new JsonException($"Error deserializing random settings {e.Name} {e.Id}")))
            .ToArray();
    }

    public async Task<IEnumerable<MatchLink>> GetMatchLinksByFromMatchName(Guid tournamentId, Name fromMatchName,
        CancellationToken cancellationToken)
    {
        var nameString = fromMatchName.ToString();
        return await _dbContext.MatchLink.Where(m => m.TournamentId == tournamentId && m.FromMatch == nameString)
            .Select(e => new MatchLink(e.FromMatch, e.ToMatch, e.IsWinners, e.PlayerCount))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveMatchLink(Guid tournamentId, MatchLink matchLink, CancellationToken cancellationToken)
    {
        var fromString = matchLink.FromMatch.ToString();
        var toString = matchLink.ToMatch.ToString();
        var entity = await _dbContext.MatchLink.Where(m =>
                m.TournamentId == tournamentId && m.FromMatch == fromString && m.ToMatch == toString)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity == null)
        {
            await _dbContext.MatchLink.AddAsync(new MatchLinkEntity
            {
                TournamentId = tournamentId,
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

    public async Task DeleteMatchLink(Guid tournamentId, Name fromName, Name toName,
        CancellationToken cancellationToken)
    {
        var fromString = fromName.ToString();
        var toString = toName.ToString();
        var entity = await _dbContext.MatchLink.Where(m =>
                m.TournamentId == tournamentId && m.FromMatch == fromString && m.ToMatch == toString)
            .FirstOrDefaultAsync(cancellationToken);
        if (entity != null)
        {
            _dbContext.MatchLink.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<MatchLink>> GetAllMatchLinks(Guid tournamentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.MatchLink
            .Where(t => t.TournamentId == tournamentId)
            .Select(ml => new MatchLink(ml.FromMatch, ml.ToMatch, ml.IsWinners, ml.PlayerCount))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IEnumerable<MatchPlayer>> GetMatchPlayers(Guid tournamentId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.TournamentPlayer.Where(t => t.TournamentId == tournamentId).Select(t =>
                new MatchPlayer(t.PlayerName, t.Seed, t.DiscordId, t.Notes, t.PotentialConflict))
            .ToArrayAsync(cancellationToken);
    }

    public async Task SaveMatchPlayer(Guid tournamentId, MatchPlayer player, CancellationToken cancellationToken)
    {
        var nameString = player.Name.ToString();
        var entity = await _dbContext.TournamentPlayer.FirstOrDefaultAsync(
            t => t.TournamentId == tournamentId && t.PlayerName == nameString, cancellationToken);
        if (entity == null)
        {
            await _dbContext.TournamentPlayer.AddAsync(new TournamentPlayerEntity
            {
                DiscordId = player.DiscordId,
                Notes = player.Notes,
                PlayerName = player.Name,
                PotentialConflict = player.PotentialConflict,
                Seed = player.Seed,
                TournamentId = tournamentId
            }, cancellationToken);
        }
        else
        {
            entity.DiscordId = player.DiscordId;
            entity.Notes = player.Notes;
            entity.PlayerName = player.Name;
            entity.PotentialConflict = player.PotentialConflict;
            entity.Seed = player.Seed;
            entity.TournamentId = tournamentId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteMatchPlayer(Guid tournamentId, Name playerName, CancellationToken cancellationToken)
    {
        var nameString = playerName.ToString();
        var entities = await _dbContext.TournamentPlayer
            .Where(t => t.TournamentId == tournamentId && t.PlayerName == nameString)
            .ToArrayAsync(cancellationToken);
        _dbContext.TournamentPlayer.RemoveRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private string MachineCacheKey(Guid tournamentId)
    {
        return $"{nameof(EFMatchRepository)}__Machines__{tournamentId}";
    }

    public async Task<IEnumerable<MatchMachineRecord>> GetMachines(Guid tournamentId,
        CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync(MachineCacheKey(tournamentId), async o =>
        {
            o.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);
            return await _dbContext.TournamentMachine.Where(t => t.TournamentId == tournamentId)
                .Select(t => new MatchMachineRecord(t.MachineName, t.Priority, t.IsWarmup))
                .ToArrayAsync(cancellationToken);
        });
    }

    public async Task SaveMachine(Guid tournamentId, MatchMachineRecord machine,
        CancellationToken cancellationToken)
    {
        var machineName = machine.MachineName.ToString();
        var entity = await _dbContext.TournamentMachine.FirstOrDefaultAsync(t => t.TournamentId == tournamentId
            && t.MachineName == machineName, cancellationToken);
        if (entity == null)
        {
            await _dbContext.TournamentMachine.AddAsync(new TournamentMachineEntity
            {
                IsWarmup = machine.IsWarmup,
                Priority = machine.Priority,
                MachineName = machineName,
                TournamentId = tournamentId
            }, cancellationToken);
        }
        else
        {
            entity.Priority = machine.Priority;
            entity.IsWarmup = machine.IsWarmup;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _cache.Remove(MachineCacheKey(tournamentId));
    }

    public async Task DeleteMachine(Guid tournamentId, Name machineName, CancellationToken cancellationToken)
    {
        var machineNameString = machineName.ToString();
        var entity = await _dbContext.TournamentMachine.FirstOrDefaultAsync(t => t.TournamentId == tournamentId
            && t.MachineName == machineNameString, cancellationToken);
        if (entity != null)
        {
            _dbContext.TournamentMachine.Remove(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _cache.Remove(MachineCacheKey(tournamentId));
        }
    }
}