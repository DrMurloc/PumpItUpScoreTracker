using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFPlayerFolderLevelRepository : IPlayerFolderLevelRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPlayerFolderLevelRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IEnumerable<FolderLevelRecord>> GetFolderLevels(MixEnum mix, Guid userId,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<PlayerFolderLevelEntity>()
            .Where(e => e.UserId == userId && e.MixId == mixId)
            .ToArrayAsync(cancellationToken);

        // A row whose ChartType no longer parses belongs to a retired type — skip it rather than
        // throw, the same way milestone reads tolerate unknown kinds.
        return rows
            .Select(e => Enum.TryParse<ChartType>(e.ChartType, out var type)
                ? new FolderLevelRecord(mix, type, DifficultyLevel.From(e.Level), e.Size, e.Played, e.AverageScore)
                : null)
            .Where(r => r != null)
            .Cast<FolderLevelRecord>();
    }

    public async Task Save(Guid userId, IEnumerable<FolderLevelRecord> levels, DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        var writes = levels.ToArray();
        if (writes.Length == 0) return;

        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        foreach (var mixGroup in writes.GroupBy(l => l.Mix))
        {
            var mixId = MixIds.For(mixGroup.Key);
            var levelNumbers = mixGroup.Select(l => (int)l.Level).Distinct().ToArray();
            var existing = await database.Set<PlayerFolderLevelEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && levelNumbers.Contains(e.Level))
                .ToArrayAsync(cancellationToken);

            foreach (var level in mixGroup)
            {
                var typeName = level.Type.ToString();
                var row = existing.FirstOrDefault(e => e.ChartType == typeName && e.Level == (int)level.Level);
                if (row == null)
                {
                    row = new PlayerFolderLevelEntity
                    {
                        UserId = userId, MixId = mixId, ChartType = typeName, Level = (int)level.Level
                    };
                    await database.AddAsync(row, cancellationToken);
                }
                else if (row.Size == level.Size && row.Played == level.Played &&
                         row.AverageScore == level.AverageScore)
                {
                    // Nothing moved — leave UpdatedAt alone so it keeps meaning "last changed".
                    continue;
                }

                row.Size = level.Size;
                row.Played = level.Played;
                row.AverageScore = level.AverageScore;
                row.UpdatedAt = asOf;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
