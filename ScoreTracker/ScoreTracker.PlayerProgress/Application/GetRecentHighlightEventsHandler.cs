using MediatR;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Rebuilds <see cref="ScoreHighlightsCapturedEvent" />s from the persisted highlight + milestone
///     tables so the community feed can be backfilled without re-importing scores. One event per
///     (user, mix, session). The highlight table stores neither score nor plate, so each highlighted
///     chart is enriched from the player's current best — that gives the score the feed shows and the
///     plate the policy needs to detect PGs (one best-scores read per user, admin one-shot). Read-only.
/// </summary>
internal sealed class GetRecentHighlightEventsHandler
    : IRequestHandler<GetRecentHighlightEventsQuery, IEnumerable<ScoreHighlightsCapturedEvent>>
{
    private readonly IScoreHighlightRepository _highlights;
    private readonly IPlayerMilestoneRepository _milestones;
    private readonly IScoreReader _scores;

    public GetRecentHighlightEventsHandler(IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones, IScoreReader scores)
    {
        _highlights = highlights;
        _milestones = milestones;
        _scores = scores;
    }

    public async Task<IEnumerable<ScoreHighlightsCapturedEvent>> Handle(GetRecentHighlightEventsQuery request,
        CancellationToken cancellationToken)
    {
        var highlightsBySession = (await _highlights.GetHighlightsSince(request.Since, cancellationToken))
            .GroupBy(h => (h.UserId, h.Mix, Session: h.Record.SessionId!.Value))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var milestonesBySession = (await _milestones.GetFeedMilestonesSince(request.Since, cancellationToken))
            .GroupBy(m => (m.UserId, m.Mix, Session: m.Record.SessionId!.Value))
            .ToDictionary(g => g.Key, g => g.ToArray());

        // Current best per (user, mix) — the highlight table has no score/plate, so enrich from it.
        var bestByUserMix = new Dictionary<(Guid UserId, MixEnum Mix), IReadOnlyDictionary<Guid, RecordedPhoenixScore>>();
        foreach (var (userId, mix) in highlightsBySession.Keys.Select(k => (k.UserId, k.Mix)).Distinct())
            bestByUserMix[(userId, mix)] = (await _scores.GetBestScores(mix, userId, cancellationToken))
                .ToDictionary(s => s.ChartId);

        var events = new List<ScoreHighlightsCapturedEvent>();
        foreach (var key in highlightsBySession.Keys.Union(milestonesBySession.Keys))
        {
            var hs = highlightsBySession.GetValueOrDefault(key,
                Array.Empty<(Guid UserId, MixEnum Mix, ScoreHighlightRecord Record)>());
            var ms = milestonesBySession.GetValueOrDefault(key,
                Array.Empty<(Guid UserId, MixEnum Mix, PlayerMilestoneRecord Record)>());
            var best = bestByUserMix.GetValueOrDefault((key.UserId, key.Mix))
                       ?? new Dictionary<Guid, RecordedPhoenixScore>();

            var changes = hs.Select(h =>
            {
                var record = best.GetValueOrDefault(h.Record.ChartId);
                return new ScoreHighlightsCapturedEvent.HighlightedChange(
                    h.Record.ChartId, IsNewPass: false, OldScore: null,
                    NewScore: record?.Score is { } phoenix ? (int)phoenix : null,
                    Plate: record?.Plate?.GetName(),
                    IsBroken: record?.IsBroken ?? false,
                    h.Record.Flags, h.Record.Detail);
            }).ToArray();
            var milestones = ms.Select(m => m.Record).ToArray();
            var occurredAt = hs.Select(h => h.Record.OccurredAt)
                .Concat(ms.Select(m => m.Record.OccurredAt))
                .Max();

            // EventId = SessionId keeps the backfill idempotent across re-runs.
            events.Add(new ScoreHighlightsCapturedEvent(key.Session, occurredAt, key.UserId, key.Mix,
                key.Session, changes, milestones, Array.Empty<TitleProgressDelta>()));
        }

        return events;
    }
}
