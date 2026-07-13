using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFPlayerMilestoneRepository : IPlayerMilestoneRepository
{
    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPlayerMilestoneRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task Append(MixEnum mix, Guid userId, IEnumerable<PlayerMilestoneWrite> milestones,
        CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        foreach (var milestone in milestones)
            await database.AddAsync(new PlayerMilestoneEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MixId = mixId,
                SessionId = milestone.SessionId,
                OccurredAt = milestone.OccurredAt,
                Kind = milestone.Kind.ToString(),
                OldValue = milestone.OldValue,
                NewValue = milestone.NewValue,
                Title = milestone.Title,
                Detail = milestone.Detail
            }, cancellationToken);

        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<PlayerMilestoneRecord>> GetMilestones(MixEnum mix, Guid userId,
        DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken)
    {
        var mixId = MixIds.For(mix);
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PlayerMilestoneEntity>()
                .Where(e => e.UserId == userId && e.MixId == mixId && e.OccurredAt >= since && e.OccurredAt <= until)
                .ToArrayAsync(cancellationToken))
            // Unknown kinds (from a future version's rows) are skipped rather than thrown on.
            .Select(e => Enum.TryParse<MilestoneKind>(e.Kind, out var kind)
                ? new PlayerMilestoneRecord(kind, e.SessionId, e.OccurredAt, e.OldValue, e.NewValue, e.Title,
                    e.Detail)
                : null)
            .Where(r => r != null)
            .Cast<PlayerMilestoneRecord>();
    }

    public async Task<IEnumerable<PlayerMilestoneRecord>> GetMilestonesBySessions(Guid userId,
        IEnumerable<Guid> sessionIds, CancellationToken cancellationToken)
    {
        var ids = sessionIds.Distinct().Select(s => (Guid?)s).ToArray();
        if (ids.Length == 0) return Array.Empty<PlayerMilestoneRecord>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return (await database.Set<PlayerMilestoneEntity>()
                .Where(e => e.UserId == userId && ids.Contains(e.SessionId))
                .ToArrayAsync(cancellationToken))
            .Select(e => Enum.TryParse<MilestoneKind>(e.Kind, out var kind)
                ? new PlayerMilestoneRecord(kind, e.SessionId, e.OccurredAt, e.OldValue, e.NewValue, e.Title,
                    e.Detail)
                : null)
            .Where(r => r != null)
            .Cast<PlayerMilestoneRecord>();
    }

    public async Task<IEnumerable<(Guid UserId, MixEnum Mix, PlayerMilestoneRecord Record)>> GetTitleCompletionsSince(
        DateTimeOffset since, CancellationToken cancellationToken)
    {
        var kind = MilestoneKind.TitleCompleted.ToString();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<PlayerMilestoneEntity>()
            .Where(e => e.OccurredAt >= since && e.Kind == kind && e.SessionId != null)
            .ToArrayAsync(cancellationToken);
        return rows.Select(e => (e.UserId, MixIds.ToEnum(e.MixId),
            new PlayerMilestoneRecord(MilestoneKind.TitleCompleted, e.SessionId, e.OccurredAt, e.OldValue,
                e.NewValue, e.Title, e.Detail)));
    }
}
