using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class SessionBreakdownBuilderTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AChartPlayedSeveralTimesInOneSessionBuildsEveryRow()
    {
        // The shape a session with attempts always has: six losing plays and the clear that
        // ended them, all on one chart. Treating the row list as one-per-chart threw here.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = Enumerable.Range(0, 6)
            .Select(i => Row(chart.Id, Start.AddMinutes(i * 7), 400000 + i * 60000, broken: true,
                ScoreEventClassification.Played))
            .Append(Row(chart.Id, Start.AddMinutes(45), 912400, false, ScoreEventClassification.NewPass))
            .ToArray();

        var model = await Build(chart, rows);

        Assert.NotNull(model.Hero);
        Assert.Equal(7, model.Hero!.Scores.Count);
    }

    [Fact]
    public async Task EachRowWearsTheLiveStandingOfItsOwnScore()
    {
        // The standing is read per (chart, score), so a chart played twice does not hand the
        // earlier row the later score's place — and a break carries none at all.
        var chart = ChartAt(ChartType.Single, 21);
        var pass = Row(chart.Id, Start, 905000, false, ScoreEventClassification.NewPass);
        var upscore = RowFrom(chart.Id, 931000, previousBest: 905000) with { OccurredAt = Start.AddHours(1) };
        var broke = Row(chart.Id, Start.AddMinutes(30), 400000, true, ScoreEventClassification.Played);
        var standings = new Dictionary<ScoreOnChart, PeerStanding>
        {
            [new ScoreOnChart(chart.Id, 905000)] = new(50, 40, 20, 0, 0, Array.Empty<PeerStandingSource>(), null),
            [new ScoreOnChart(chart.Id, 931000)] = new(50, 40, 5, 0, 0, Array.Empty<PeerStandingSource>(), null)
        };

        var (_, model) = await BuildWith(chart, new[] { pass, broke, upscore }, standings: standings);

        var byTime = model.Hero!.Scores.OrderBy(s => s.Row.OccurredAt).ToArray();
        Assert.Equal(21, byTime[0].Standing!.Place);
        Assert.Null(byTime[1].Standing);
        Assert.Equal(6, byTime[2].Standing!.Place);
    }

    [Fact]
    public async Task PagingTheHistoryLeavesTheHeroExactlyWhereItWas()
    {
        // The hero is not what you paged. Rebuilding it is both wasted work and the reason the
        // interaction used to look like a navigation.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows);

        var paged = await builder.Refilter(model, User, 2, 20, null, CancellationToken.None);

        Assert.Same(model.Hero, paged.Hero);
    }

    [Fact]
    public async Task PromotingACardLeavesTheHistoryExactlyWhereItWas()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows);

        var reselected = await builder.Reselect(model, User, Session, 1, 20, null, CancellationToken.None);

        Assert.Same(model.History, reselected.History);
        Assert.NotNull(reselected.Hero);
    }

    [Fact]
    public async Task AFreshSessionWithNothingCapturedYetIsPending()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows,
            captured: false, sessionEndedMinutesAgo: 0);

        Assert.True(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task ASessionOlderThanTheWindowStopsClaimingToBeCalculating()
    {
        // A session that genuinely earned nothing looks identical to one still being worked out.
        // The window is what stops the page telling that player to keep waiting forever.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows,
            captured: false, sessionEndedMinutesAgo: 30);

        Assert.False(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task ASessionWithCapturedRowsShowsNoCardButStaysWatchable()
    {
        // The regression this pins: capture writes in several passes, so a page opening between
        // two of them has rows and shows no card — but the window must stay open, or the page
        // sits on half a session until someone reloads it by hand. Whether to show the card and
        // whether to keep watching are different questions.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows,
            captured: true, sessionEndedMinutesAgo: 0);

        Assert.False(model.Hero!.CapturePending);
        Assert.True(model.Hero.CaptureWindowOpen);
        Assert.True(model.Hero.CapturedRows > 0);
    }

    [Fact]
    public async Task ASessionPredatingTheSessionTableIsNeverPending()
    {
        // No ScoreSession row means no wall clock to test against — and those sessions are
        // historical by definition, so "still calculating" could never be true of them.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, captured: false);

        Assert.False(model.Hero!.CapturePending);
    }

    [Fact]
    public async Task APhoenix2ScorePastYourPhoenix1BestReportsHowFarPast()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Equal(20000, model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task AnEarlierPhoenix2ScoreAlreadyPastPhoenix1SpendsTheMark()
    {
        // The mark is the moment you passed your old self, so it must not ride every later
        // upscore on a chart you already took.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { RowFrom(chart.Id, 975000, previousBest: 950000) };

        var model = await Build(chart, rows, MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task APhoenixSessionNeverComparesAgainstPhoenix1()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, MixEnum.Phoenix,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task ABrokenPhoenix1RecordIsNotABestToHavePassed()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 960000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000, broken: true) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task MatchingYourPhoenix1BestIsNotPassingIt()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 940000, false, ScoreEventClassification.NewPass) };

        var model = await Build(chart, rows, MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

        Assert.Null(model.Hero!.Scores.Single().Phoenix1Gain);
    }

    [Fact]
    public async Task AHighlightPinsToTheRowThatEarnedItNotToEveryAttempt()
    {
        // The repeated-play bug: four stage breaks before the clear each wore the pass's
        // medals, because highlights joined by chart id (D45).
        var chart = ChartAt(ChartType.Single, 21);
        var rows = Enumerable.Range(0, 4)
            .Select(i => Row(chart.Id, Start.AddMinutes(i * 7), 400000, broken: true,
                ScoreEventClassification.Break))
            .Append(Row(chart.Id, Start.AddMinutes(45), 912400, false, ScoreEventClassification.NewPass))
            .ToArray();

        var model = await Build(chart, rows);

        var flagged = Assert.Single(model.Hero!.Scores, s => s.IsFlagged);
        Assert.Equal(ScoreEventClassification.NewPass, flagged.Row.Classification);
        Assert.All(model.Hero.Scores.Where(s => s.Row.IsBroken), s =>
        {
            Assert.Equal(HighlightFlags.None, s.Flags);
            Assert.Null(s.Detail);
        });
    }

    [Fact]
    public async Task TwoCapturesForOneChartPinInOrderOntoItsRecordRows()
    {
        // A pass in one batch and an upscore in a later one are two captures; each belongs to
        // its own row, not merged across both.
        var chart = ChartAt(ChartType.Single, 21);
        var pass = Row(chart.Id, Start, 905000, false, ScoreEventClassification.NewPass);
        var upscore = RowFrom(chart.Id, 931000, previousBest: 905000) with { OccurredAt = Start.AddHours(1) };
        var highlights = new[]
        {
            new ScoreHighlightRecord(chart.Id, Session, Start.AddMinutes(2), HighlightFlags.FolderDebut, 21,
                21.0, new HighlightDetail(AttemptsBeforeClear: 3)),
            new ScoreHighlightRecord(chart.Id, Session, Start.AddMinutes(62), HighlightFlags.PumbilityTop50,
                21, 21.4, new HighlightDetail(PumbilityRank: 40))
        };

        var model = await Build(chart, new[] { pass, upscore },
            highlights: highlights);

        var byTime = model.Hero!.Scores.OrderBy(s => s.Row.OccurredAt).ToArray();
        Assert.Equal(HighlightFlags.FolderDebut, byTime[0].Flags);
        Assert.Equal(3, byTime[0].Detail!.AttemptsBeforeClear);
        Assert.Equal(HighlightFlags.PumbilityTop50, byTime[1].Flags);
        Assert.Equal(40, byTime[1].Detail!.PumbilityRank);
    }

    [Fact]
    public async Task MoreCapturesThanRecordRowsMergeOntoTheLast()
    {
        // One batch's capture describes its final state, so when captures outnumber record
        // rows the extras land on the newest row — never spread backwards onto attempts.
        var chart = ChartAt(ChartType.Single, 21);
        var upscore = RowFrom(chart.Id, 931000, previousBest: 905000);
        var highlights = new[]
        {
            new ScoreHighlightRecord(chart.Id, Session, Start.AddMinutes(2), HighlightFlags.FolderDebut, 21,
                21.0, new HighlightDetail(AttemptsBeforeClear: 3)),
            new ScoreHighlightRecord(chart.Id, Session, Start.AddMinutes(4), HighlightFlags.PumbilityTop50,
                21, 21.4, new HighlightDetail(PumbilityRank: 40, PeerPercentile: 0.9))
        };

        var model = await Build(chart, new[] { upscore },
            highlights: highlights);

        var row = model.Hero!.Scores.Single();
        Assert.Equal(HighlightFlags.FolderDebut | HighlightFlags.PumbilityTop50, row.Flags);
        Assert.Equal(40, row.Detail!.PumbilityRank);
    }

    [Fact]
    public async Task ACaptureWhoseChartHasNoRecordRowShowsNowhere()
    {
        // Better no medal than a medal on a stage break — the one wrong place it used to go.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 400000, broken: true, ScoreEventClassification.Break) };

        var model = await Build(chart, rows);

        Assert.DoesNotContain(model.Hero!.Scores, s => s.IsFlagged);
        Assert.All(model.Hero.Scores, s => Assert.Null(s.Detail));
    }

    private static async Task<SessionsPageModel> Build(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null, ScoreHighlightRecord[]? highlights = null)
    {
        return (await BuildWith(chart, rows, mix, phoenix1, captured, sessionEndedMinutesAgo, highlights))
            .Model;
    }

    private static async Task<(SessionBreakdownBuilder Builder, SessionsPageModel Model)> BuildWith(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null, ScoreHighlightRecord[]? highlights = null,
        IReadOnlyDictionary<ScoreOnChart, PeerStanding>? standings = null)
    {
        var mediator = new Mock<IMediator>();
        var group = new RecentSessionsPage.SessionGroup(Session, null, mix, "officialImport",
            rows.Min(r => r.OccurredAt), rows.Max(r => r.OccurredAt), rows);

        // Wall clock, deliberately distinct from the journal's play date: "is capture still
        // running" is a question about when the scores reached us.
        var now = Start.AddHours(4);
        var sessions = sessionEndedMinutesAgo == null
            ? Array.Empty<ScoreSessionRecord>()
            : new[]
            {
                new ScoreSessionRecord(Session, User, mix, "officialImport", "SHIRONEKO", "2",
                    now.AddMinutes(-sessionEndedMinutesAgo.Value - 5),
                    now.AddMinutes(-sessionEndedMinutesAgo.Value), rows.Length, 1, 0)
            };

        Setup(mediator, new GetRecentSessionsQuery(User, 1, 20),
            new RecentSessionsPage(1, new[] { group }));
        Setup(mediator, new GetScoreSessionsQuery(User), (IReadOnlyList<ScoreSessionRecord>)sessions);
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        mediator.Setup(m => m.Send(It.IsAny<GetScoreHighlightsForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(highlights ?? (captured
                ? new[]
                {
                    new ScoreHighlightRecord(chart.Id, Session, Start, HighlightFlags.FolderDebut, 21, 21.0,
                        new HighlightDetail(PeerPercentile: 0.4, AttemptsBeforeClear: 6))
                }
                : Array.Empty<ScoreHighlightRecord>()));
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerMilestonesForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerMilestoneRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPeerStandingsForScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(standings ?? new Dictionary<ScoreOnChart, PeerStanding>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(User, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                22.6, 22.6, 23.4));

        var ledger = new Mock<IScoreReader>();
        ledger.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(phoenix1 ?? Array.Empty<UserPhoenixScore>());
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(now);
        var builder = new SessionBreakdownBuilder(mediator.Object, ledger.Object, clock.Object);
        return (builder, await builder.Build(User, null, 1, 20, null, CancellationToken.None));
    }

    private static void Setup<T>(Mock<IMediator> mediator, IRequest<T> request, T result)
    {
        mediator.Setup(m => m.Send(It.Is<IRequest<T>>(r => r.GetType() == request.GetType()),
            It.IsAny<CancellationToken>())).ReturnsAsync(result);
    }

    private static Chart ChartAt(ChartType type, int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null);
    }

    private static RecentSessionsPage.ScoreEventRecord Row(Guid chartId, DateTimeOffset at, int score,
        bool broken, ScoreEventClassification classification)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, at, score, broken ? null : "Fair Game",
            broken, "seed", Session, classification, null);
    }

    private static RecentSessionsPage.ScoreEventRecord RowFrom(Guid chartId, int score, int previousBest)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, Start, score, "Fair Game", false, "seed",
            Session, ScoreEventClassification.Upscore, previousBest);
    }

    private static UserPhoenixScore Phoenix1(Guid chartId, int score, bool broken = false)
    {
        return new UserPhoenixScore(User, chartId, Name.From("DrMurloc"), PhoenixScore.From(score),
            PhoenixPlate.FairGame, broken);
    }

}
