using MassTransit;
using MediatR;
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
///     The write side of March of Murlocs (docs/design/march-of-murlocs.md §11.4): a draft is opened,
///     filled by hand or from the journal, published onto a board, and deleted. A draft <em>is</em> a
///     session with no publication stamp (D17), so there is one row and one lifecycle rather than a
///     staging table.
///     <para>
///         Every command loads the session, applies the rule inside <see cref="TournamentSession" />
///         and writes the whole thing back: the aggregate owns the window predicate and D45's
///         keep-the-better rule, so no handler re-implements either. Ownership is checked on load —
///         a draft is private, and a published session belongs to whoever recorded it.
///     </para>
/// </summary>
internal sealed partial class MoMDraftHandler :
    IRequestHandler<CreateMoMDraftCommand, Guid>,
    IRequestHandler<AddMoMDraftChartCommand, MoMEntryResult>,
    IRequestHandler<RemoveMoMDraftChartCommand>,
    IRequestHandler<SetMoMDraftVideoCommand>,
    IRequestHandler<PublishMoMSessionCommand>,
    IRequestHandler<DeleteMoMSessionCommand>,
    IRequestHandler<GetMoMDraftQuery, MoMDraftView?>
{
    private readonly IMoMRepository _write;
    private readonly IMoMReadRepository _read;
    private readonly IChartRepository _charts;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IDateTimeOffsetAccessor _dateTime;
    private readonly IBus _bus;

    /// <summary>
    ///     The score journal, read through the Ledger's published port rather than its query: an
    ///     EventCompetition -> ScoreLedger reference would close a cycle through Communities and
    ///     Randomizer, which is the case published ports exist for.
    /// </summary>
    private readonly IScoreReader _scores;

    public MoMDraftHandler(IMoMRepository write, IMoMReadRepository read, IChartRepository charts,
        ICurrentUserAccessor currentUser, IDateTimeOffsetAccessor dateTime, IBus bus, IScoreReader scores)
    {
        _write = write;
        _read = read;
        _charts = charts;
        _currentUser = currentUser;
        _dateTime = dateTime;
        _bus = bus;
        _scores = scores;
    }

    public async Task<Guid> Handle(CreateMoMDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.User.Id;
        var existing = await _write.GetDraftId(request.BoardId, userId, cancellationToken);
        if (existing is { } open) return open;

        var board = await _read.GetBoard(request.BoardId, cancellationToken)
                    ?? throw new ArgumentException("That board does not exist.");

        var sessionId = Guid.NewGuid();
        await _write.SaveSession(sessionId, new TournamentSession(userId, board.Configuration, board.Mix),
            cancellationToken);
        return sessionId;
    }

    public async Task<MoMEntryResult> Handle(AddMoMDraftChartCommand request, CancellationToken cancellationToken)
    {
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        if (loaded == null) return new MoMEntryResult(MoMEntryOutcome.Rejected, null);

        var chart = (await _charts.GetCharts(loaded.Board.Mix, chartIds: new[] { request.ChartId },
            cancellationToken: cancellationToken)).FirstOrDefault();
        if (chart == null || !loaded.Session.CanAdd(chart))
            return new MoMEntryResult(MoMEntryOutcome.Rejected, null);

        // Read the held score before the add, because a replacement overwrites it in place.
        var previous = Held(loaded.Session, chart)?.Score;
        var outcome = loaded.Session.Add(chart, request.Score, request.Plate, request.IsBroken);
        await _write.SaveSession(request.SessionId, loaded.Session, cancellationToken);

        return new MoMEntryResult(Outcome(outcome), previous);
    }

    public async Task Handle(RemoveMoMDraftChartCommand request, CancellationToken cancellationToken)
    {
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        if (loaded == null || request.Ordinal < 0 || request.Ordinal >= loaded.Session.Entries.Count) return;

        loaded.Session.Remove(loaded.Session.Entries[request.Ordinal]);
        await _write.SaveSession(request.SessionId, loaded.Session, cancellationToken);
    }

    public async Task Handle(SetMoMDraftVideoCommand request, CancellationToken cancellationToken)
    {
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        if (loaded == null) return;

        loaded.Session.VideoUrl = Uri.TryCreate(request.Url, UriKind.Absolute, out var url) ? url : null;
        await _write.SaveSession(request.SessionId, loaded.Session, cancellationToken);
    }

    public async Task Handle(PublishMoMSessionCommand request, CancellationToken cancellationToken)
    {
        var loaded = await LoadDraft(request.SessionId, cancellationToken);
        // An empty session has nothing to rank, and a published one is frozen (D17).
        if (loaded == null || loaded.Session.Entries.Count == 0) return;

        var now = _dateTime.Now;
        await _write.PublishSession(request.SessionId, now, cancellationToken);
        await _bus.Publish(
            new MoMSessionPublishedEvent(request.SessionId, loaded.Stored.BoardId, loaded.Stored.UserId, now),
            cancellationToken);
    }

    public async Task Handle(DeleteMoMSessionCommand request, CancellationToken cancellationToken)
    {
        // Published or not: discarding a draft and taking a session off a board are the same act.
        var stored = await Mine(request.SessionId, cancellationToken);
        if (stored == null) return;

        await _write.DeleteSession(request.SessionId, cancellationToken);
    }

    public async Task<MoMDraftView?> Handle(GetMoMDraftQuery request, CancellationToken cancellationToken)
    {
        var loaded = await Load(request.SessionId, cancellationToken);
        if (loaded == null) return null;

        var seasons = await _read.GetSeasons(cancellationToken);
        var season = seasons.FirstOrDefault(s => s.Id == loaded.Board.SeasonId);
        var snapshot = loaded.Board.Configuration.Scoring.ChartLevelSnapshot;
        var charts = loaded.Session.Entries.Select(e => new MoMSessionChart(e.Chart, e.Score, e.Plate,
                e.IsBroken, e.SessionScore, e.BonusPoints,
                snapshot != null && snapshot.TryGetValue(e.Chart.Id, out var level)
                    ? level
                    : (int)e.Chart.Level + .5,
                e.PlayedAt))
            .ToArray();

        var songTime = loaded.Session.TotalPlayTime;
        return new MoMDraftView(
            loaded.Stored.Id,
            loaded.Board.Id,
            loaded.Board.SeasonId,
            season?.Name ?? string.Empty,
            loaded.Board.Mix,
            loaded.Board.ChartType,
            loaded.Stored.PublishedAt != null,
            loaded.Board.Configuration.MaxTime,
            songTime,
            charts.Length == 0 ? TimeSpan.Zero : songTime - charts[^1].Chart.Song.Duration,
            loaded.Session.TotalScore,
            loaded.Session.VideoUrl,
            charts);
    }

    /// <summary>The session, its board and the aggregate — for anything that may edit it.</summary>
    private sealed record LoadedSession(MoMStoredSession Stored, MoMBoardInfo Board, TournamentSession Session);

    /// <summary>A session the signed-in player owns, or null. Ownership is the only gate a draft has.</summary>
    private async Task<MoMStoredSession?> Mine(Guid sessionId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsLoggedIn) return null;

        var stored = await _read.GetSession(sessionId, cancellationToken);
        return stored != null && stored.UserId == _currentUser.User.Id ? stored : null;
    }

    /// <summary>Loads for editing: null when it is not yours, or when it is already published and frozen.</summary>
    private async Task<LoadedSession?> LoadDraft(Guid sessionId, CancellationToken cancellationToken)
    {
        var loaded = await Load(sessionId, cancellationToken);
        return loaded is { Stored.PublishedAt: null } ? loaded : null;
    }

    private async Task<LoadedSession?> Load(Guid sessionId, CancellationToken cancellationToken)
    {
        var stored = await Mine(sessionId, cancellationToken);
        if (stored == null) return null;

        var board = await _read.GetBoard(stored.BoardId, cancellationToken);
        if (board == null) return null;

        var rows = (await _read.GetSessionCharts(new[] { sessionId }, cancellationToken))
            .OrderBy(r => r.Ordinal).ToArray();
        var charts = (await _charts.GetCharts(board.Mix,
                chartIds: rows.Select(r => r.ChartId).Distinct().ToArray(),
                cancellationToken: cancellationToken))
            .ToDictionary(c => c.Id);

        // Rebuilt through the entries constructor rather than replayed through Add: a session whose
        // closing chart overhangs the window would fail its own CanAdd on the way back in, and the
        // stored points are what the board ranked.
        var entries = rows.Where(r => charts.ContainsKey(r.ChartId))
            .Select(r => new TournamentSession.Entry(charts[r.ChartId], r.Score, r.Plate, r.IsBroken,
                r.SessionScore, r.BonusPoints, r.PlayedAt))
            .ToArray();
        var session = new TournamentSession(stored.UserId, board.Configuration, entries, board.Mix)
        {
            VideoUrl = stored.VideoUrl
        };

        return new LoadedSession(stored, board, session);
    }

    private static TournamentSession.Entry? Held(TournamentSession session, Chart chart) =>
        session.Entries.FirstOrDefault(e => e.Chart.Level == chart.Level && e.Chart.Type == chart.Type &&
                                            e.Chart.Song.Name == chart.Song.Name);

    private static MoMEntryOutcome Outcome(TournamentSession.AddOutcome outcome) => outcome switch
    {
        TournamentSession.AddOutcome.Added => MoMEntryOutcome.Added,
        TournamentSession.AddOutcome.Replaced => MoMEntryOutcome.Replaced,
        _ => MoMEntryOutcome.Kept
    };
}
