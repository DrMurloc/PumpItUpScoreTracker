using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScoreTracker.Application.Queries;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFRandomizerRepository(ChartAttemptDbContext database,
        IOptions<JsonSerializerOptions> jsonOptions, ICurrentUserAccessor currentUser) : IRandomizerRepository,
        IRequestHandler<GetRandomSettingsQuery, IEnumerable<SavedRandomizerSettings>>
    {
        public async Task SaveSettings(Guid userId, Name settingsName, RandomSettings settings,
            CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entity = await database.UserRandomSettings.Where(u => u.UserId == userId && u.Name == nameString)
                .FirstOrDefaultAsync(cancellationToken);

            if (entity == null)
                await database.UserRandomSettings.AddAsync(new UserRandomSettingsEntity
                {
                    UserId = userId,
                    Json = JsonSerializer.Serialize(settings)
                }, cancellationToken);
            else
                entity.Json = JsonSerializer.Serialize(settings);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteSettings(Guid userId, Name settingsName, CancellationToken cancellationToken)
        {
            var nameString = settingsName.ToString();
            var entities = await database.UserRandomSettings.Where(e => e.UserId == userId && e.Name == nameString)
                .ToArrayAsync(cancellationToken);
            database.UserRandomSettings.RemoveRange(entities);
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<SavedRandomizerSettings>> Handle(GetRandomSettingsQuery request,
            CancellationToken cancellationToken)
        {
            var userId = currentUser.User.Id;
            return (await database.UserRandomSettings.Where(u => u.UserId == userId).ToArrayAsync(cancellationToken))
                .Select(u => new SavedRandomizerSettings(u.Name,
                    JsonSerializer.Deserialize<RandomSettings>(u.Json, jsonOptions.Value) ?? new RandomSettings()));
        }
    }
}
