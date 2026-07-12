using ScoreTracker.Randomizer.Infrastructure.Entities;
using ScoreTracker.Randomizer.Domain;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.Randomizer.Contracts;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Randomizer.Infrastructure
{
    internal sealed class EFRandomizerRepository(IDbContextFactory<ChartAttemptDbContext> factory,
        IOptions<JsonSerializerOptions> jsonOptions, ICurrentUserAccessor currentUser) : IRandomizerRepository,
        IRequestHandler<GetRandomSettingsQuery, IEnumerable<SavedRandomizerSettings>>,
        IRequestHandler<GetTournamentRandomSettingsQuery, IEnumerable<SavedRandomizerSettings>>,
        IRequestHandler<GetSharedSettingsQuery, SavedRandomizerSettings?>
    {
        public async Task SaveSettings(Guid userId, Name settingsName, RandomSettings settings, MixEnum mix,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var nameString = settingsName.ToString();
            var entity = await database.Set<UserRandomSettingsEntity>().Where(u => u.UserId == userId && u.Name == nameString)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
            {
                await database.Set<UserRandomSettingsEntity>().AddAsync(new UserRandomSettingsEntity
                {
                    Name = nameString,
                    UserId = userId,
                    Json = JsonSerializer.Serialize(settings),
                    Mix = mix.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.Json = JsonSerializer.Serialize(settings);
                entity.Mix = mix.ToString();
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteSettings(Guid userId, Name settingsName, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var nameString = settingsName.ToString();
            var entities = await database.Set<UserRandomSettingsEntity>().Where(e => e.UserId == userId && e.Name == nameString)
                .ToArrayAsync(cancellationToken);
            database.Set<UserRandomSettingsEntity>().RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task SaveTournamentSettings(Guid tournamentId, Name settingsName, RandomSettings settings,
            MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var nameString = settingsName.ToString();
            var entity = await database.Set<TournamentRandomSettingsEntity>()
                .Where(t => t.TournamentId == tournamentId && t.Name == nameString)
                .FirstOrDefaultAsync(cancellationToken);
            if (entity == null)
            {
                await database.Set<TournamentRandomSettingsEntity>().AddAsync(new TournamentRandomSettingsEntity
                {
                    TournamentId = tournamentId,
                    Name = nameString,
                    Json = JsonSerializer.Serialize(settings),
                    Mix = mix.ToString()
                }, cancellationToken);
            }
            else
            {
                entity.Json = JsonSerializer.Serialize(settings);
                entity.Mix = mix.ToString();
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteTournamentSettings(Guid tournamentId, Name settingsName,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var nameString = settingsName.ToString();
            var entities = await database.Set<TournamentRandomSettingsEntity>()
                .Where(t => t.TournamentId == tournamentId && t.Name == nameString)
                .ToArrayAsync(cancellationToken);
            database.Set<TournamentRandomSettingsEntity>().RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<Guid> EnsureShareToken(Guid userId, Name settingsName, CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var nameString = settingsName.ToString();
            var entity = await database.Set<UserRandomSettingsEntity>()
                .Where(u => u.UserId == userId && u.Name == nameString)
                .FirstAsync(cancellationToken);
            if (entity.ShareToken == null)
            {
                entity.ShareToken = Guid.NewGuid();
                await database.SaveChangesAsync(cancellationToken);
            }

            return entity.ShareToken.Value;
        }

        public async Task<IEnumerable<SavedRandomizerSettings>> Handle(GetRandomSettingsQuery request,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var userId = currentUser.User.Id;
            return (await database.Set<UserRandomSettingsEntity>().Where(u => u.UserId == userId).ToArrayAsync(cancellationToken))
                .Select(ToRecord);
        }

        public async Task<IEnumerable<SavedRandomizerSettings>> Handle(GetTournamentRandomSettingsQuery request,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            return (await database.Set<TournamentRandomSettingsEntity>()
                    .Where(t => t.TournamentId == request.TournamentId)
                    .ToArrayAsync(cancellationToken))
                .Select(t => new SavedRandomizerSettings(t.Name,
                    JsonSerializer.Deserialize<RandomSettings>(t.Json, jsonOptions.Value) ?? new RandomSettings(),
                    ParseMix(t.Mix)));
        }

        public async Task<SavedRandomizerSettings?> Handle(GetSharedSettingsQuery request,
            CancellationToken cancellationToken)
        {
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            var entity = await database.Set<UserRandomSettingsEntity>()
                .FirstOrDefaultAsync(u => u.ShareToken == request.ShareToken, cancellationToken);
            return entity == null ? null : ToRecord(entity);
        }

        private SavedRandomizerSettings ToRecord(UserRandomSettingsEntity entity)
        {
            return new SavedRandomizerSettings(entity.Name,
                JsonSerializer.Deserialize<RandomSettings>(entity.Json, jsonOptions.Value) ?? new RandomSettings(),
                ParseMix(entity.Mix));
        }

        private static MixEnum ParseMix(string mix)
        {
            return Enum.TryParse<MixEnum>(mix, out var parsed) ? parsed : MixEnum.Phoenix;
        }
    }
}
