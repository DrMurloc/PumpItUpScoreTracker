using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFQualifiersRepository : IQualifiersRepository
    {
        private readonly ChartAttemptDbContext _database;

        public EFQualifiersRepository(ChartAttemptDbContext database)
        {
            _database = database;
        }

        public async Task<UserQualifiers?> GetQualifiers(Name userName, QualifiersConfiguration config,
            CancellationToken cancellationToken = default)
        {
            var nameString = userName.ToString();
            var entity =
                await _database.UserQualifier.FirstOrDefaultAsync(u => u.Name == nameString, cancellationToken);
            return entity == null ? null : From(entity, config);
        }

        private static UserQualifiers From(UserQualifierEntity entity, QualifiersConfiguration config)
        {
            var entries = JsonSerializer.Deserialize<QualifierSubmissionDto[]>(entity.Entries);
            return new UserQualifiers(config, entity.IsApproved, entity.Name, entries.ToDictionary(e => e.ChartId, e =>
                new UserQualifiers.Submission
                {
                    ChartId = e.ChartId,
                    PhotoUrl = new Uri(e.PhotoUrl),
                    Score = e.Score
                }));
        }

        public async Task SaveQualifiers(UserQualifiers qualifiers, CancellationToken cancellationToken = default)
        {
            var nameString = qualifiers.UserName.ToString();
            var entity =
                await _database.UserQualifier.FirstOrDefaultAsync(u => u.Name == nameString, cancellationToken);
            var entryJson = JsonSerializer.Serialize(qualifiers.Submissions.Select(kv => new QualifierSubmissionDto
            {
                ChartId = kv.Value.ChartId,
                PhotoUrl = kv.Value.PhotoUrl.ToString(),
                Score = kv.Value.Score
            }));
            if (entity == null)
            {
                await _database.AddAsync(new UserQualifierEntity
                {
                    Id = Guid.NewGuid(),
                    Entries = entryJson,
                    IsApproved = qualifiers.IsApproved,
                    Name = nameString,
                }, cancellationToken);
            }
            else
            {
                entity.IsApproved = qualifiers.IsApproved;
                entity.Entries = entryJson;
            }

            await _database.UserQualifierHistory.AddAsync(new UserQualifierHistoryEntity
            {
                Id = Guid.NewGuid(),
                Entries = entryJson,
                IsApproved = qualifiers.IsApproved,
                Name = nameString,
                RecordedDate = DateTimeOffset.Now
            }, cancellationToken);

            await _database.SaveChangesAsync(cancellationToken);
        }

        public async Task<IEnumerable<UserQualifiers>> GetAllUserQualifiers(QualifiersConfiguration config,
            CancellationToken cancellationToken = default)
        {
            var entities = await _database.UserQualifier.ToArrayAsync(cancellationToken);
            return entities.Select(e => From(e, config)).ToArray();
        }
    }
}
