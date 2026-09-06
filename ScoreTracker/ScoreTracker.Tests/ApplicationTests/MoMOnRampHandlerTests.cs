using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The My Sessions on-ramp (docs/design/march-of-murlocs.md D32): the quiet link is always
///     offered because there is always a season, the loud callout only when a window inside the
///     night holds one chart type with under fifty minutes of rest, and a night already on a board
///     wears the chip instead of either.
/// </summary>
public sealed class MoMOnRampHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Night = new(2026, 8, 8, 5, 20, 0, TimeSpan.Zero);
    private readonly MoMReadHandlerFixture _fixture = new();
    private readonly List<ScoreJournalEntry> _journal = new();
    private readonly Guid _me = Guid.NewGuid();

    private MoMOnRampHandler Handler()
    {
        var read = new Mock<IMoMReadRepository>();
        read.Setup(m => m.GetSeasons(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _fixture.Seasons.ToArray());
        read.Setup(m => m.GetBoards(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Boards.Where(b => ids.Contains(b.SeasonId)).ToArray());
        read.Setup(m => m.GetPublishedSessions(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Sessions.Where(s => s.PublishedAt != null && ids.Contains(s.BoardId)).ToArray());
        read.Setup(m => m.GetSessionCharts(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                _fixture.Rows.Where(r => ids.Contains(r.SessionId)).ToArray());

        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                _fixture.Charts.Where(c => ids == null || ids.Contains(c.Id)).ToArray());

        var scores = new Mock<IScoreReader>();
        // The real query caps from the NEWEST end, so the stub does too: a handler that asks only
        // for everything since a night began must come back empty-handed for an older night.
        scores.Setup(s => s.GetRecentPlays(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<DateTimeOffset?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, Guid _, DateTimeOffset since, DateTimeOffset? until, int limit,
                    CancellationToken _) =>
                _journal.Where(e => e.OccurredAt >= since && (until == null || e.OccurredAt <= until))
                    .OrderByDescending(e => e.OccurredAt)
                    .Take(limit)
                    .OrderBy(e => e.OccurredAt)
                    .ToArray());

        return new MoMOnRampHandler(read.Object, charts.Object, scores.Object, FakeDateTime.At(Now).Object);
    }

    private MoMSeason LiveSeason()
    {
        var season = _fixture.Season("Summer 2026", Now.AddMonths(-1), Now.AddMonths(2));
        _fixture.Board(season, ChartType.Double);
        _fixture.Board(season, ChartType.Single);
        return season;
    }

    /// <summary>A run of two-minute Doubles charts back to back, which is a session by any measure.</summary>
    private void PlayRun(int charts, int gapSeconds = 30, ChartType type = ChartType.Double)
    {
        for (var i = 0; i < charts; i++)
        {
            var chart = _fixture.Chart($"Chart {type} {i}", 20, 120, type);
            _journal.Add(new ScoreJournalEntry(Night.AddSeconds(i * (120 + gapSeconds)),
                ScoreJournalEntry.OfficialImportSource, _me, chart.Id, 980000, PhoenixPlate.MarvelousGame,
                false, MixEnum.Phoenix));
        }
    }

    private Task<EventCompetition.Contracts.MoMOnRamp?> Detect() =>
        Handler().Handle(new DetectMoMSessionQuery(_me, MixEnum.Phoenix, Night, Night.AddHours(4)),
            CancellationToken.None);

    [Fact]
    public async Task WithNoLiveSeasonThereIsNothingToOffer()
    {
        _fixture.Season("Winter 2025", Now.AddYears(-1), Now.AddMonths(-10));

        Assert.Null(await Detect());
    }

    [Fact]
    public async Task ANightTooRestfulToBeASessionStillGetsTheQuietLinksBoard()
    {
        LiveSeason();
        // Six charts across four hours: no 1:45 window holds under fifty minutes of rest.
        PlayRun(6, gapSeconds: 2000);

        var onRamp = await Detect();

        Assert.NotNull(onRamp);
        Assert.Null(onRamp!.Candidate);
        Assert.Null(onRamp.Recorded);
        Assert.NotNull(onRamp.RecordBoardId);
    }

    [Fact]
    public async Task ASessionShapedNightNamesTheBoardAndTheNumbersThatDecidedIt()
    {
        LiveSeason();
        // Thirty two-minute charts thirty seconds apart: an hour of song inside the window.
        PlayRun(30);

        var onRamp = await Detect();

        var candidate = onRamp!.Candidate;
        Assert.NotNull(candidate);
        Assert.Equal(ChartType.Double, candidate!.ChartType);
        Assert.Equal(_fixture.Boards.Single(b => b.ChartType == ChartType.Double).Id, candidate.BoardId);
        Assert.True(candidate.Rest < TimeSpan.FromMinutes(50));
        Assert.True(candidate.Charts >= 30);
        Assert.Equal(candidate.BoardId, onRamp.RecordBoardId);
    }

    [Fact]
    public async Task TheCandidateFollowsTheChartTypeThatWasActuallyPlayed()
    {
        LiveSeason();
        PlayRun(30, type: ChartType.Single);

        var onRamp = await Detect();

        Assert.Equal(ChartType.Single, onRamp!.Candidate!.ChartType);
        Assert.Equal(_fixture.Boards.Single(b => b.ChartType == ChartType.Single).Id, onRamp.Candidate.BoardId);
    }

    [Fact]
    public async Task ANightAlreadyOnABoardWearsTheChipAndOffersNoCandidate()
    {
        var season = LiveSeason();
        PlayRun(30);
        var board = _fixture.Boards.Single(b => b.ChartType == ChartType.Double);
        var me = _fixture.User("DRMURLOC");
        var mine = _fixture.Session(board, me with { Id = _me }, 59319, Now.AddDays(-6));
        // One chart row stamped inside the night is what ties the session to it.
        _fixture.Rows.Add(new MoMStoredSessionChart(mine.Id, 0, _fixture.Charts[0].Id, 980000,
            PhoenixPlate.MarvelousGame, false, 900, 0, Night.AddMinutes(5)));
        // Someone else scored higher, so the chip should say second.
        _fixture.Session(board, _fixture.User("RIVAL"), 70000, Now.AddDays(-5));

        var onRamp = await Detect();

        Assert.Null(onRamp!.Candidate);
        var recorded = onRamp.Recorded;
        Assert.NotNull(recorded);
        Assert.Equal(mine.Id, recorded!.SessionId);
        Assert.Equal(2, recorded.Place);
        Assert.Equal(2, recorded.Of);
        Assert.Equal(59319, recorded.TotalScore);
        Assert.Equal(ChartType.Double, recorded.ChartType);
    }

    [Fact]
    public async Task AHandEnteredSessionNeverClaimsANightItMayNotBe()
    {
        var season = LiveSeason();
        PlayRun(30);
        var board = _fixture.Boards.Single(b => b.ChartType == ChartType.Double);
        var mine = _fixture.Session(board, _fixture.User("DRMURLOC") with { Id = _me }, 59319, Now.AddDays(-6));
        // No PlayedAt: typed by hand, so nothing ties it to this night.
        _fixture.Rows.Add(new MoMStoredSessionChart(mine.Id, 0, _fixture.Charts[0].Id, 980000,
            PhoenixPlate.MarvelousGame, false, 900, 0, null));

        var onRamp = await Detect();

        Assert.Null(onRamp!.Recorded);
        Assert.NotNull(onRamp.Candidate);
    }

    [Fact]
    public async Task SomeoneElsesSessionOnTheSameBoardIsNotYourNight()
    {
        LiveSeason();
        PlayRun(30);
        var board = _fixture.Boards.Single(b => b.ChartType == ChartType.Double);
        var theirs = _fixture.Session(board, _fixture.User("SOMEONE_ELSE"), 70000, Now.AddDays(-6));
        _fixture.Rows.Add(new MoMStoredSessionChart(theirs.Id, 0, _fixture.Charts[0].Id, 980000,
            PhoenixPlate.MarvelousGame, false, 900, 0, Night.AddMinutes(5)));

        var onRamp = await Detect();

        Assert.Null(onRamp!.Recorded);
        Assert.NotNull(onRamp.Candidate);
    }

    [Fact]
    public async Task ANightBuriedUnderLaterPlaysIsStillFound()
    {
        LiveSeason();
        PlayRun(30);
        // Everything since that night, newest first, is four hundred plays of something else. A read
        // bounded only at the old end hands back none of the night at all.
        var later = _fixture.Chart("Since Then", 20, 120);
        for (var i = 0; i < 400; i++)
            _journal.Add(new ScoreJournalEntry(Night.AddDays(1).AddSeconds(i * 150),
                ScoreJournalEntry.OfficialImportSource, _me, later.Id, 980000, PhoenixPlate.MarvelousGame,
                false, MixEnum.Phoenix));

        var onRamp = await Detect();

        Assert.NotNull(onRamp!.Candidate);
        Assert.Equal(30, onRamp.Candidate!.Charts);
    }
}
