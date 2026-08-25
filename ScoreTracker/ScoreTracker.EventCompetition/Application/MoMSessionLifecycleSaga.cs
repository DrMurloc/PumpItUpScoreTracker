using MassTransit;
using MediatR;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Events;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The D17 lifecycle: draft → published → frozen. A save replays the whole entry list
///     under the board's frozen rules (window, repeats, worthless charts — the same
///     TournamentSession that governs manual entry), recomputes the derived cache columns,
///     and never publishes; publishing stamps the recorded date (D18) and fires the bus
///     event exactly once; a correction is delete-and-resubmit, so delete works on drafts
///     (Discard) and published sessions alike. One open draft per board per player (§10) —
///     a save without a session id lands on it.
/// </summary>
internal sealed class MoMSessionLifecycleSaga :
    IRequestHandler<SaveMoMSessionDraftCommand, MoMSessionView>,
    IRequestHandler<PublishMoMSessionCommand>,
    IRequestHandler<DeleteMoMSessionCommand>
{
    private readonly IBus _bus;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IMediator _mediator;
    private readonly IMoMRepository _mom;

    public MoMSessionLifecycleSaga(IMoMRepository mom, IChartRepository charts,
        ICurrentUserAccessor currentUser, IDateTimeOffsetAccessor dateTime, IBus bus,
        IMediator mediator)
    {
        _mom = mom;
        _charts = charts;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _bus = bus;
        _mediator = mediator;
    }

    public async Task<MoMSessionView> Handle(SaveMoMSessionDraftCommand request,
        CancellationToken cancellationToken)
    {
        var user = _currentUser.User;
        var configuration = await _mom.GetBoardConfiguration(request.BoardId, true,
                                cancellationToken)
                            ?? throw new MoMSessionRuleException("This board does not exist.");
        GuardSeasonLive(configuration);

        Guid sessionId;
        if (request.SessionId != null)
        {
            var stored = await _mom.GetSession(request.SessionId.Value, cancellationToken)
                         ?? throw new MoMSessionRuleException("This session does not exist.");
            if (stored.UserId != user.Id && !user.IsAdmin)
                throw new NotAuthorizedException("edit this session");
            if (stored.BoardId != request.BoardId)
                throw new MoMSessionRuleException("This session belongs to a different board.");
            if (stored.PublishedAt != null)
                throw new MoMSessionRuleException(
                    "A published session cannot be edited. Delete it and record a new one.");
            sessionId = stored.Id;
        }
        else
        {
            // One open draft per board (§10): a save without an id lands on it rather than
            // opening a second workspace. Guid.Empty asks storage to mint the id.
            var existing = await _mom.GetDraft(request.BoardId, user.Id, cancellationToken);
            sessionId = existing?.Id ?? Guid.Empty;
        }

        var session = new TournamentSession(user.Id, configuration, configuration.Scoring.Mix);
        if (request.Entries.Count > 0)
        {
            var charts = (await _charts.GetCharts(configuration.Scoring.Mix,
                    chartIds: request.Entries.Select(e => e.ChartId).Distinct().ToArray(),
                    cancellationToken: cancellationToken))
                .ToDictionary(c => c.Id);
            foreach (var entry in request.Entries)
            {
                if (!charts.TryGetValue(entry.ChartId, out var chart))
                    throw new MoMSessionRuleException(
                        "One of the charts in this session no longer exists on this mix.");
                session.Add(chart, entry.Score, entry.Plate, entry.IsBroken, entry.PlayedAt);
            }
        }

        session.VideoUrl = request.VideoUrl;
        var now = _dateTime.Now;
        var savedId = await _mom.UpsertSession(
            ToRecord(sessionId, request.BoardId, user.Id, session,
                await _mom.GetSeasonSnapshot(request.BoardId, cancellationToken),
                configuration.Scoring.Mix),
            session.Entries.Select((e, ordinal) => new MoMSessionChartRecord(ordinal, e.Chart.Id,
                e.Score, e.Plate.ToString(), e.IsBroken, e.SessionScore, e.BonusPoints,
                e.PlayedAt)).ToArray(),
            now, cancellationToken);

        return await _mediator.Send(new GetMoMSessionQuery(savedId), cancellationToken)
               ?? throw new InvalidOperationException(
                   $"Draft {savedId} vanished between save and read");
    }

    public async Task Handle(PublishMoMSessionCommand request, CancellationToken cancellationToken)
    {
        var user = _currentUser.User;
        var session = await _mom.GetSession(request.SessionId, cancellationToken)
                      ?? throw new MoMSessionRuleException("This session does not exist.");
        if (session.UserId != user.Id && !user.IsAdmin)
            throw new NotAuthorizedException("publish this session");
        if (session.PublishedAt != null)
            throw new MoMSessionRuleException("This session is already published.");
        if (session.ChartsPlayed == 0)
            throw new MoMSessionRuleException("An empty session cannot be published.");
        var configuration = await _mom.GetBoardConfiguration(session.BoardId, false,
                                cancellationToken)
                            ?? throw new MoMSessionRuleException("This board does not exist.");
        GuardSeasonLive(configuration);

        await _mom.PublishSession(session.Id, _dateTime.Now, cancellationToken);
        await _bus.Publish(new MoMSessionPublishedEvent(session.Id, session.BoardId,
            session.UserId), cancellationToken);
    }

    public async Task Handle(DeleteMoMSessionCommand request, CancellationToken cancellationToken)
    {
        var user = _currentUser.User;
        var session = await _mom.GetSession(request.SessionId, cancellationToken);
        if (session == null) return;
        if (session.UserId != user.Id && !user.IsAdmin)
            throw new NotAuthorizedException("delete this session");
        await _mom.DeleteSession(session.Id, cancellationToken);
    }

    private void GuardSeasonLive(TournamentConfiguration configuration)
    {
        var now = _dateTime.Now;
        if (configuration.StartDate != null && now < configuration.StartDate)
            throw new MoMSessionRuleException("This season has not started yet.");
        if (configuration.EndDate != null && now > configuration.EndDate)
            throw new MoMSessionRuleException(
                "This season has ended — sessions can no longer be recorded on it.");
    }

    private static MoMSessionRecord ToRecord(Guid sessionId, Guid boardId, Guid userId,
        TournamentSession session, IReadOnlyDictionary<Guid, double> snapshot, MixEnum mix)
    {
        // The derived cache columns (§6), computed exactly as the repointed save always did:
        // balanced level is the snapshot override where one exists, folder + 0.5 where none.
        var entries = session.Entries;
        return new MoMSessionRecord(sessionId, boardId, userId, null,
            session.TotalScore,
            entries.Count,
            session.CurrentRestTime.Ticks,
            entries.Count == 0
                ? 0
                : entries.Average(e => snapshot.TryGetValue(e.Chart.Id, out var balanced)
                    ? balanced
                    : (int)e.Chart.Level + 0.5),
            entries.Count == 0
                ? 0
                : entries.Average(e => (int)e.Score.LetterGradeFor(mix)),
            entries.Count == 0 ? 0 : entries.Min(e => (int)e.Chart.Level),
            entries.Count == 0 ? 0 : entries.Max(e => (int)e.Chart.Level),
            session.VideoUrl?.ToString());
    }
}
