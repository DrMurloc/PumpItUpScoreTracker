using MediatR;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;

namespace ScoreTracker.PlayerProgress.Application;

/// <summary>
///     Rebuilds <see cref="ScoreHighlightsCapturedEvent" />s from the persisted highlight + milestone
///     tables so the community feed can be backfilled without re-importing scores. One event per
///     (user, mix, session); changes carry no plate (see the query doc). Read-only.
/// </summary>
internal sealed class GetRecentHighlightEventsHandler
    : IRequestHandler<GetRecentHighlightEventsQuery, IEnumerable<ScoreHighlightsCapturedEvent>>
{
    private readonly IScoreHighlightRepository _highlights;
    private readonly IPlayerMilestoneRepository _milestones;

    public GetRecentHighlightEventsHandler(IScoreHighlightRepository highlights,
        IPlayerMilestoneRepository milestones)
    {
        _highlights = highlights;
        _milestones = milestones;
    }

    public async Task<IEnumerable<ScoreHighlightsCapturedEvent>> Handle(GetRecentHighlightEventsQuery request,
        CancellationToken cancellationToken)
    {
        var highlightsBySession = (await _highlights.GetHighlightsSince(request.Since, cancellationToken))
            .GroupBy(h => (h.UserId, h.Mix, Session: h.Record.SessionId!.Value))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var titlesBySession = (await _milestones.GetTitleCompletionsSince(request.Since, cancellationToken))
            .GroupBy(t => (t.UserId, t.Mix, Session: t.Record.SessionId!.Value))
            .ToDictionary(g => g.Key, g => g.ToArray());

        var events = new List<ScoreHighlightsCapturedEvent>();
        foreach (var key in highlightsBySession.Keys.Union(titlesBySession.Keys))
        {
            var hs = highlightsBySession.GetValueOrDefault(key,
                Array.Empty<(Guid UserId, MixEnum Mix, ScoreHighlightRecord Record)>());
            var ts = titlesBySession.GetValueOrDefault(key,
                Array.Empty<(Guid UserId, MixEnum Mix, PlayerMilestoneRecord Record)>());

            var changes = hs.Select(h => new ScoreHighlightsCapturedEvent.HighlightedChange(
                    h.Record.ChartId, IsNewPass: false, OldScore: null, NewScore: null, Plate: null,
                    IsBroken: false, h.Record.Flags, h.Record.Detail))
                .ToArray();
            var milestones = ts.Select(t => t.Record).ToArray();
            var occurredAt = hs.Select(h => h.Record.OccurredAt)
                .Concat(ts.Select(t => t.Record.OccurredAt))
                .Max();

            // EventId = SessionId keeps the backfill idempotent across re-runs.
            events.Add(new ScoreHighlightsCapturedEvent(key.Session, occurredAt, key.UserId, key.Mix,
                key.Session, changes, milestones, Array.Empty<TitleProgressDelta>()));
        }

        return events;
    }
}
