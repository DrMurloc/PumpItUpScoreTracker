using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Data.Persistence;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.PlayerProgress.Infrastructure.Entities;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Infrastructure;

internal sealed class EFPlayerHighlightRepository : IPlayerHighlightRepository
{
    // Enums ride as strings so a reordered WinKind can't silently reshuffle stored payloads.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFPlayerHighlightRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<bool> Add(Guid eventId, Guid userId, MixEnum mix, DateTimeOffset occurredAt,
        Guid? sessionId, IReadOnlyList<SignificantWin> wins, CancellationToken cancellationToken)
    {
        if (wins.Count == 0) return false;
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        if (await database.Set<PlayerHighlightEntity>()
                .AnyAsync(h => h.EventId == eventId, cancellationToken))
            return false;

        await database.Set<PlayerHighlightEntity>().AddAsync(new PlayerHighlightEntity
        {
            EventId = eventId,
            UserId = userId,
            MixId = MixIds.For(mix),
            OccurredAt = occurredAt,
            SessionId = sessionId,
            Payload = JsonSerializer.Serialize(wins, SerializerOptions),
            SchemaVersion = PlayerHighlightSchema.CurrentVersion
        }, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PlayerHighlightEntry>> GetForUsers(IReadOnlyCollection<Guid> userIds,
        MixEnum mix, int take, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0 || take <= 0) return Array.Empty<PlayerHighlightEntry>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var rows = await database.Set<PlayerHighlightEntity>()
            .AsNoTracking()
            .Where(h => h.MixId == mixId
                        && h.SchemaVersion == PlayerHighlightSchema.CurrentVersion
                        && userIds.Contains(h.UserId))
            .OrderByDescending(h => h.OccurredAt)
            .Take(take)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Deserialize).ToArray();
    }

    public async Task<IReadOnlyList<PlayerHighlightEntry>> GetForEvents(IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0) return Array.Empty<PlayerHighlightEntry>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<PlayerHighlightEntity>()
            .AsNoTracking()
            .Where(h => eventIds.Contains(h.EventId)
                        && h.SchemaVersion == PlayerHighlightSchema.CurrentVersion)
            .ToArrayAsync(cancellationToken);
        return rows.Select(Deserialize).ToArray();
    }

    public async Task<int> PurgeBefore(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<PlayerHighlightEntity>()
            .Where(h => h.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static PlayerHighlightEntry Deserialize(PlayerHighlightEntity row)
    {
        return new PlayerHighlightEntry(row.EventId, row.UserId, MixIds.ToEnum(row.MixId), row.OccurredAt,
            row.SessionId,
            JsonSerializer.Deserialize<List<SignificantWin>>(row.Payload, SerializerOptions)
            ?? new List<SignificantWin>());
    }
}
