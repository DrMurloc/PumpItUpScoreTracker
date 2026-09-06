using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Commands;
using ScoreTracker.EventCompetition.Contracts.Events;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The draft lifecycle (docs/design/march-of-murlocs.md §11.4): opening a draft, filling it by
///     hand or from the journal, publishing it onto a board, and deleting it. The write repository is
///     stubbed to round-trip into the same lists the read side answers from, so a command's effect is
///     visible to the next read exactly as it would be through storage.
/// </summary>
public sealed class MoMDraftHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 3, 0, 0, TimeSpan.Zero);
    private readonly MoMReadHandlerFixture _fixture = new();
    private readonly Mock<IMoMRepository> _write = new();
    private readonly Mock<IBus> _bus = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly List<ScoreJournalEntry> _journal = new();
    private User _signedIn = null!;
    private bool _loggedIn = true;

    private MoMDraftHandler Handler()
    {
        var read = ReadRepository();

        _write.Setup(w => w.GetDraftId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid boardId, Guid userId, CancellationToken _) => _fixture.Sessions
                .Where(s => s.BoardId == boardId && s.UserId == userId && s.PublishedAt == null)
                .Select(s => (Guid?)s.Id).FirstOrDefault());

        // Writes land in the fixture's lists, derived columns and all, the way the repository does.
        _write.Setup(w => w.SaveSession(It.IsAny<Guid>(), It.IsAny<TournamentSession>(),
                It.IsAny<CancellationToken>()))
            .Callback((Guid id, TournamentSession session, CancellationToken _) =>
            {
                var existing = _fixture.Sessions.FirstOrDefault(s => s.Id == id);
                _fixture.Sessions.RemoveAll(s => s.Id == id);
                _fixture.Rows.RemoveAll(r => r.SessionId == id);
                _fixture.Sessions.Add(new MoMStoredSession(id, session.TournamentId, session.UsersId,
                    existing?.PublishedAt, session.TotalScore, session.Entries.Count, session.CurrentRestTime,
                    session.Entries.Count == 0 ? 0 : session.Entries.Average(e => (int)e.Chart.Level + .5),
                    session.Entries.Count == 0 ? 0 : session.Entries.Average(e => (int)e.Score.LetterGradeFor(session.Mix)),
                    session.Entries.Count == 0 ? 0 : session.Entries.Min(e => (int)e.Chart.Level),
                    session.Entries.Count == 0 ? 0 : session.Entries.Max(e => (int)e.Chart.Level),
                    session.VideoUrl, existing?.CreatedAt ?? Now));
                _fixture.Rows.AddRange(session.Entries.Select((e, ordinal) => new MoMStoredSessionChart(id,
                    ordinal, e.Chart.Id, e.Score, e.Plate, e.IsBroken, e.SessionScore, e.BonusPoints, e.PlayedAt)));
            })
            .Returns(Task.CompletedTask);

        _write.Setup(w => w.PublishSession(It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback((Guid id, DateTimeOffset at, CancellationToken _) =>
            {
                var existing = _fixture.Sessions.FirstOrDefault(s => s.Id == id);
                if (existing is not { PublishedAt: null }) return;
                _fixture.Sessions.Remove(existing);
                _fixture.Sessions.Add(existing with { PublishedAt = at });
            })
            .Returns(Task.CompletedTask);

        _write.Setup(w => w.DeleteSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback((Guid id, CancellationToken _) =>
            {
                _fixture.Sessions.RemoveAll(s => s.Id == id);
                _fixture.Rows.RemoveAll(r => r.SessionId == id);
            })
            .Returns(Task.CompletedTask);

        _scores.Setup(s => s.GetRecentPlays(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _journal.OrderBy(e => e.OccurredAt).ToArray());

        var currentUser = new Mock<ICurrentUserAccessor>();
        currentUser.SetupGet(c => c.IsLoggedIn).Returns(() => _loggedIn);
        currentUser.SetupGet(c => c.User).Returns(() => _signedIn);

        return new MoMDraftHandler(_write.Object, read, Charts(), currentUser.Object,
            FakeDateTime.At(Now).Object, _bus.Object, _scores.Object);
    }

    private IMoMReadRepository ReadRepository()
    {
        var read = new Mock<IMoMReadRepository>();
        read.Setup(m => m.GetSeasons(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fixture.Seasons.ToArray());
        read.Setup(m => m.GetBoard(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _fixture.Boards.FirstOrDefault(b => b.Id == id));
        read.Setup(m => m.GetSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => _fixture.Sessions.FirstOrDefault(s => s.Id == id));
        read.Setup(m => m.GetSessionCharts(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Rows.Where(r => ids.Contains(r.SessionId)).ToArray());
        return read.Object;
    }

    private IChartRepository Charts()
    {
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                _fixture.Charts.Where(c => ids == null || ids.Contains(c.Id)).ToArray());
        return charts.Object;
    }

    private MoMBoardInfo Board(ChartType type = ChartType.Double)
    {
        var season = _fixture.Season("Summer 2026", Now.AddMonths(-1), Now.AddMonths(2));
        _signedIn = _fixture.User("DRMURLOC");
        return _fixture.Board(season, type);
    }

    private void Play(Chart chart, int score, DateTimeOffset at, bool stageBroken = false)
    {
        _journal.Add(new ScoreJournalEntry(at, ScoreJournalEntry.OfficialImportSource, _signedIn.Id, chart.Id,
            stageBroken ? null : score, PhoenixPlate.MarvelousGame, stageBroken, MixEnum.Phoenix,
            IsStageBroken: stageBroken));
    }

    // ---- opening and owning a draft -----------------------------------------------------------

    [Fact]
    public async Task OpeningADraftTwiceResumesTheOneAlreadyOpen()
    {
        var board = Board();
        var handler = Handler();

        var first = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var second = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(_fixture.Sessions);
        Assert.Null(_fixture.Sessions.Single().PublishedAt);
    }

    [Fact]
    public async Task APublishedSessionDoesNotCountAsAnOpenDraft()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Slam", 24).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        var next = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);

        Assert.NotEqual(draft, next);
        Assert.Equal(2, _fixture.Sessions.Count);
    }

    [Fact]
    public async Task SomeoneElsesDraftIsNotReadableAndNotEditable()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var chart = _fixture.Chart("Slam", 24);

        _signedIn = _fixture.User("SOMEONE_ELSE");

        Assert.Null(await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None));
        var result = await handler.Handle(
            new AddMoMDraftChartCommand(draft, chart.Id, 980000, PhoenixPlate.MarvelousGame, false),
            CancellationToken.None);
        Assert.Equal(MoMEntryOutcome.Rejected, result.Outcome);
        await handler.Handle(new DeleteMoMSessionCommand(draft), CancellationToken.None);
        Assert.Single(_fixture.Sessions);
    }

    [Fact]
    public async Task ASignedOutVisitorSeesNoDraftAtAll()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);

        _loggedIn = false;

        Assert.Null(await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None));
    }

    // ---- entering plays by hand (D45) ---------------------------------------------------------

    [Fact]
    public async Task ARepeatKeepsTheBetterPlayAndNamesTheScoreItDisplaced()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var uglyDee = _fixture.Chart("Ugly Dee", 17, 96);

        var first = await handler.Handle(
            new AddMoMDraftChartCommand(draft, uglyDee.Id, 969366, PhoenixPlate.MarvelousGame, false),
            CancellationToken.None);
        var better = await handler.Handle(
            new AddMoMDraftChartCommand(draft, uglyDee.Id, 970915, PhoenixPlate.MarvelousGame, false),
            CancellationToken.None);
        var worse = await handler.Handle(
            new AddMoMDraftChartCommand(draft, uglyDee.Id, 900000, PhoenixPlate.SuperbGame, false),
            CancellationToken.None);

        Assert.Equal(MoMEntryOutcome.Added, first.Outcome);
        Assert.Null(first.PreviousScore);
        Assert.Equal(MoMEntryOutcome.Replaced, better.Outcome);
        Assert.Equal(969366, (int)better.PreviousScore!.Value);
        Assert.Equal(MoMEntryOutcome.Kept, worse.Outcome);
        Assert.Equal(970915, (int)worse.PreviousScore!.Value);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        var chart = Assert.Single(view!.Charts);
        Assert.Equal(970915, (int)chart.Score);
    }

    [Fact]
    public async Task AChartIsRemovedByItsPositionAndAStrayPositionIsIgnored()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Slam", 24).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Gargoyle", 20).Id, 986121,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);

        await handler.Handle(new RemoveMoMDraftChartCommand(draft, 7), CancellationToken.None);
        Assert.Equal(2, _fixture.Rows.Count);

        await handler.Handle(new RemoveMoMDraftChartCommand(draft, 0), CancellationToken.None);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        Assert.Equal("Gargoyle", Assert.Single(view!.Charts).Chart.Song.Name.ToString());
    }

    [Fact]
    public async Task TheBudgetIsSongTimeAndOnlyTheTimeBeforeTheClosingChartCanFillIt()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        // Fifty-two two-minute charts is 1:44, then a four-minute closer that starts inside.
        for (var i = 0; i < 52; i++)
            await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart($"Chart {i}", 20).Id,
                980000, PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Closer", 20, 240).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);

        Assert.Equal(53, view!.Charts.Count);
        Assert.True(view.SongTime > view.Window);
        Assert.False(view.WindowFull);

        // One more cannot start: everything before it now fills the window.
        var rejected = await handler.Handle(new AddMoMDraftChartCommand(draft,
            _fixture.Chart("One more", 20).Id, 980000, PhoenixPlate.MarvelousGame, false),
            CancellationToken.None);
        Assert.Equal(MoMEntryOutcome.Rejected, rejected.Outcome);
    }

    // ---- publishing and deleting --------------------------------------------------------------

    [Fact]
    public async Task PublishingStampsTheSessionAndAnnouncesIt()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Slam", 24).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);

        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        Assert.Equal(Now, _fixture.Sessions.Single().PublishedAt);
        _bus.Verify(b => b.Publish(It.Is<MoMSessionPublishedEvent>(e =>
            e.SessionId == draft && e.BoardId == board.Id && e.PublishedAt == Now), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AnEmptyDraftIsNotPublishable()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);

        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        Assert.Null(_fixture.Sessions.Single().PublishedAt);
        _bus.Verify(b => b.Publish(It.IsAny<MoMSessionPublishedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task APublishedSessionIsFrozenButStillReadableAndStillDeletable()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Slam", 24).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        var rejected = await handler.Handle(new AddMoMDraftChartCommand(draft,
            _fixture.Chart("Gargoyle", 20).Id, 986121, PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        Assert.Equal(MoMEntryOutcome.Rejected, rejected.Outcome);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        Assert.True(view!.IsPublished);

        await handler.Handle(new DeleteMoMSessionCommand(draft), CancellationToken.None);
        Assert.Empty(_fixture.Sessions);
    }

    [Fact]
    public async Task PublishingTwiceDoesNotMoveTheBoardPlacement()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        await handler.Handle(new AddMoMDraftChartCommand(draft, _fixture.Chart("Slam", 24).Id, 980000,
            PhoenixPlate.MarvelousGame, false), CancellationToken.None);
        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);
        var first = _fixture.Sessions.Single().PublishedAt;

        await handler.Handle(new PublishMoMSessionCommand(draft), CancellationToken.None);

        Assert.Equal(first, _fixture.Sessions.Single().PublishedAt);
    }

    // ---- importing from the journal -----------------------------------------------------------

    [Fact]
    public async Task TheDialogSplitsTheNightAndOpensOnTheBlockWorthMost()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var start = Now.AddHours(-4);
        // Two charts, a long break, then three.
        for (var i = 0; i < 2; i++) Play(_fixture.Chart($"Early {i}", 20), 970000, start.AddMinutes(i * 3));
        for (var i = 0; i < 3; i++) Play(_fixture.Chart($"Late {i}", 20), 980000, start.AddMinutes(60 + i * 3));

        var candidates = await handler.Handle(new GetMoMImportCandidatesQuery(draft), CancellationToken.None);

        Assert.Equal(5, candidates!.Plays.Count);
        Assert.Equal(2, candidates.Blocks.Count);
        Assert.Equal(2, candidates.SelectedStart);
        Assert.Equal(4, candidates.SelectedEnd);
        Assert.Equal(3, candidates.Checks.Charts);
        Assert.NotNull(candidates.Blocks[1].GapBefore);
    }

    [Fact]
    public async Task TheImportKeepsTheBetterPlayOfARepeatAndReportsWhatItDisplaced()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var start = Now.AddHours(-1);
        var uglyDee = _fixture.Chart("Ugly Dee", 17, 96);
        Play(_fixture.Chart("Gargoyle", 20, 115), 986121, start);
        Play(uglyDee, 969366, start.AddMinutes(3));
        Play(uglyDee, 970915, start.AddMinutes(6));

        var result = await handler.Handle(
            new ImportMoMDraftFromJournalCommand(draft, start.AddMinutes(-1), start.AddMinutes(10)),
            CancellationToken.None);

        Assert.Equal(2, result.Added);
        Assert.Equal(1, result.Replaced);
        Assert.Equal(0, result.Kept);
        var displaced = Assert.Single(result.Replacements);
        Assert.Equal(uglyDee.Id, displaced.ChartId);
        Assert.Equal(969366, (int)displaced.PreviousScore);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        Assert.Equal(2, view!.Charts.Count);
        Assert.Equal(970915, (int)view.Charts.Single(c => c.Chart.Id == uglyDee.Id).Score);
    }

    [Fact]
    public async Task TheImportSkipsStageBreaksTheOtherBoardsChartsAndPlaysOutsideTheRange()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var start = Now.AddHours(-1);
        Play(_fixture.Chart("Counted", 20), 980000, start);
        Play(_fixture.Chart("Arcana Force", 26), 0, start.AddMinutes(3), stageBroken: true);
        Play(_fixture.Chart("Singles thing", 20, 120, ChartType.Single), 980000, start.AddMinutes(6));
        Play(_fixture.Chart("Too late", 20), 980000, start.AddMinutes(30));

        var result = await handler.Handle(
            new ImportMoMDraftFromJournalCommand(draft, start.AddMinutes(-1), start.AddMinutes(10)),
            CancellationToken.None);

        Assert.Equal(1, result.Added);
        Assert.Equal(2, result.Skipped);
        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        Assert.Equal("Counted", Assert.Single(view!.Charts).Chart.Song.Name.ToString());
    }

    [Fact]
    public async Task AnImportedPlayCarriesTheJournalsTimestampOntoTheSession()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);
        var at = Now.AddHours(-1);
        Play(_fixture.Chart("Slam", 24), 980000, at);

        await handler.Handle(new ImportMoMDraftFromJournalCommand(draft, at.AddMinutes(-1), at.AddMinutes(1)),
            CancellationToken.None);

        var view = await handler.Handle(new GetMoMDraftQuery(draft), CancellationToken.None);
        Assert.Equal(at, Assert.Single(view!.Charts).PlayedAt);
    }

    [Fact]
    public async Task ImportingNothingLeavesTheDraftUntouched()
    {
        var board = Board();
        var handler = Handler();
        var draft = await handler.Handle(new CreateMoMDraftCommand(board.Id), CancellationToken.None);

        var result = await handler.Handle(
            new ImportMoMDraftFromJournalCommand(draft, Now.AddDays(-1), Now), CancellationToken.None);

        Assert.Equal(0, result.Added);
        Assert.Empty(_fixture.Rows);
        var candidates = await handler.Handle(new GetMoMImportCandidatesQuery(draft), CancellationToken.None);
        Assert.Empty(candidates!.Plays);
        Assert.Equal(0, candidates.Checks.Charts);
    }
}
