using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Recap;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure
{
    internal sealed class EFPlayerSeasonRecapRepository : IPlayerSeasonRecapRepository,
        IRequestHandler<GetPlayerRecapQuery, PlayerRecap?>
    {
        // Enums ride as strings so a reordered enum member can't silently reshuffle
        // every stored payload.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

        public EFPlayerSeasonRecapRepository(IDbContextFactory<ChartAttemptDbContext> factory)
        {
            _factory = factory;
        }

        public async Task SaveRecap(Guid userId, MixEnum mix, PlayerRecap recap,
            CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var payload = JsonSerializer.Serialize(recap, SerializerOptions);
            var entity = await database.Set<PlayerSeasonRecapEntity>()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MixId == mixId, cancellationToken);
            if (entity == null)
            {
                await database.AddAsync(new PlayerSeasonRecapEntity
                {
                    UserId = userId,
                    MixId = mixId,
                    Payload = payload,
                    SchemaVersion = recap.SchemaVersion,
                    ComputedAt = recap.ComputedAt
                }, cancellationToken);
            }
            else
            {
                entity.Payload = payload;
                entity.SchemaVersion = recap.SchemaVersion;
                entity.ComputedAt = recap.ComputedAt;
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<PlayerRecap?> GetRecap(Guid userId, MixEnum mix, CancellationToken cancellationToken)
        {
            await using var database = await _factory.CreateDbContextAsync(cancellationToken);
            var mixId = MixIds.For(mix);
            var entity = await database.Set<PlayerSeasonRecapEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == userId && r.MixId == mixId, cancellationToken);
            if (entity == null || entity.SchemaVersion != PlayerRecap.CurrentSchemaVersion) return null;

            return JsonSerializer.Deserialize<PlayerRecap>(entity.Payload, SerializerOptions);
        }

        public async Task<PlayerRecap?> Handle(GetPlayerRecapQuery request, CancellationToken cancellationToken)
        {
            return await GetRecap(request.UserId, request.Mix, cancellationToken);
        }
    }
}
