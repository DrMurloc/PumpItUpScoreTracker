using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;

namespace ScoreTracker.Data.Repositories
{
    public sealed class EFQualifiersRepository : IQualifiersRepository
    {
        private readonly ChartAttemptDbContext _database;
        private IChartRepository _charts;

        private static readonly ISet<Guid> ChartIds = new HashSet<Guid>(new[]
        {
            new Guid("1D1606A0-BC43-417D-8867-B574D6F3E92C"),
            new Guid("E2D622A3-ED44-456E-8572-29DA5AA90F92"),
            new Guid("0FD50D96-1F0C-4CB0-A179-9282132EF9BB"),
            new Guid("41DCE283-0C6B-4899-96DD-50CE10DC49B9"),
            new Guid("99E9BED2-3C4A-47E3-A058-ACCAE532F117"),
            new Guid("8501B01A-8D67-4CAF-AEA2-5AD0206A6255")
        });

        private static readonly IDictionary<Guid, int> Modifiers = new Dictionary<Guid, int>()
        {
            { new Guid("41DCE283-0C6B-4899-96DD-50CE10DC49B9"), 106 },
        };

        public EFQualifiersRepository(ChartAttemptDbContext database, IChartRepository charts)
        {
            _database = database;
            _charts = charts;
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

        public async Task<QualifiersConfiguration> GetQualifiersConfiguration(
            CancellationToken cancellationToken = default)
        {
            var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: ChartIds,
                cancellationToken: cancellationToken);
            return new QualifiersConfiguration(charts, Modifiers);
        }
    }
}
