using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Communities.Infrastructure.Entities;
using ScoreTracker.Data.Persistence;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.Communities.Infrastructure;

internal sealed class EFCommunityHighlightRepository : ICommunityHighlightRepository
{
    // Enums ride as strings so a reordered WinKind can't silently reshuffle stored payloads.
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IDbContextFactory<ChartAttemptDbContext> _factory;

    public EFCommunityHighlightRepository(IDbContextFactory<ChartAttemptDbContext> factory)
    {
        _factory = factory;
    }

    public async Task AddForUserCommunities(Guid eventId, Guid userId, MixEnum mix, DateTimeOffset occurredAt,
        Guid? sessionId, IReadOnlyList<SignificantWin> wins, CancellationToken cancellationToken)
    {
        if (wins.Count == 0) return;
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var communityIds = await database.Set<CommunityMembershipEntity>()
            .Where(m => m.UserId == userId)
            .Select(m => m.CommunityId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (communityIds.Length == 0) return;

        var mixId = MixIds.For(mix);
        var payload = JsonSerializer.Serialize(wins, SerializerOptions);
        var rows = communityIds.Select(communityId => new CommunityHighlightEntity
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            CommunityId = communityId,
            UserId = userId,
            MixId = mixId,
            OccurredAt = occurredAt,
            SessionId = sessionId,
            Payload = payload,
            SchemaVersion = CommunityHighlightSchema.CurrentVersion
        });
        await database.Set<CommunityHighlightEntity>().AddRangeAsync(rows, cancellationToken);
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityHighlightEntry>> GetForUser(Guid requestingUserId,
        IReadOnlyCollection<Name> communityNames, MixEnum mix, int take, CancellationToken cancellationToken)
    {
        if (communityNames.Count == 0 || take <= 0) return Array.Empty<CommunityHighlightEntry>();
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        var mixId = MixIds.For(mix);
        var nameStrings = communityNames.Select(n => n.ToString()).ToArray();

        // Rows in the requested communities where the requester is themselves a member (the consent
        // gate, CH2). One row per (event × community), so a win in several of the requester's shared
        // crews returns several rows — deduped by EventId below. Over-fetch a bounded multiple, then
        // dedupe and take: keeps a World-scoped feed from pulling the whole 30-day table.
        var fetched = await (
                from highlight in database.Set<CommunityHighlightEntity>()
                where highlight.MixId == mixId && highlight.SchemaVersion == CommunityHighlightSchema.CurrentVersion
                join community in database.Set<CommunityEntity>() on highlight.CommunityId equals community.Id
                where nameStrings.Contains(community.Name)
                join requesterMembership in database.Set<CommunityMembershipEntity>()
                    on new { highlight.CommunityId, UserId = requestingUserId }
                    equals new { requesterMembership.CommunityId, requesterMembership.UserId }
                orderby highlight.OccurredAt descending
                select new { highlight.EventId, highlight.UserId, highlight.OccurredAt, highlight.SessionId, highlight.Payload })
            .Take(Math.Max(take * 5, 100))
            .ToArrayAsync(cancellationToken);

        return fetched
            .GroupBy(r => r.EventId)
            .Select(g => g.First())
            .Take(take)
            .Select(r => new CommunityHighlightEntry(r.UserId, mix, r.OccurredAt, r.SessionId,
                JsonSerializer.Deserialize<List<SignificantWin>>(r.Payload, SerializerOptions)
                ?? new List<SignificantWin>()))
            .ToArray();
    }

    public async Task<int> PurgeBefore(DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        await using var database = await _factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<CommunityHighlightEntity>()
            .Where(h => h.OccurredAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
