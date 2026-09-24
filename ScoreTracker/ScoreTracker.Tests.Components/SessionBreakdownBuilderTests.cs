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
using ScoreTracker.Identity.Contracts.Queries;
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
    public async Task ASittingStaysPendingThroughItsQuietWindowWhereAnImportHasLongSinceSettled()
    {
        // A sitting is only announced once 15 minutes pass with nothing arriving, so ten minutes after
        // its last play its capture has not started, while an import's has long finished.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var sitting = await Build(chart, rows, captured: false, sessionEndedMinutesAgo: 10,
            source: "api:watcher-grab", newCount: 0);
        var import = await Build(chart, rows, captured: false, sessionEndedMinutesAgo: 10);

        Assert.True(sitting.Hero!.CapturePending);
        Assert.False(import.Hero!.CapturePending);
    }

    [Fact]
    public async Task AReplayedSittingWaitsOnlyForItsCaptureLikeAnyOtherSession()
    {
        // A replay sets the counts and stamps its own time as the last activity; from there the
        // capture window is an ordinary one, so a sitting whose capture wrote no rows stops waiting.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        var replayed = await Build(chart, rows, captured: false, sessionEndedMinutesAgo: 10,
            source: "api:watcher-grab", newCount: 1);

        Assert.False(replayed.Hero!.CapturePending);
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
    public async Task APlayThatIsNotAnUpscoreDoesNotEarnTheMarkOnceAnEarlierPassReachedPhoenix1()
    {
        // The repeat that earned it again on every visit: a play under the record but over the
        // Phoenix 1 best, on a chart an earlier Phoenix 2 pass had already taken past it.
        var chart = ChartAt(ChartType.Single, 21);
        var repeat = Row(chart.Id, Start, 955000, false, ScoreEventClassification.Played) with
        {
            PreviousBest = 960000
        };

        var model = await Build(chart, new[] { repeat }, MixEnum.Phoenix2,
            new[] { Phoenix1(chart.Id, 940000) });

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

    [Fact]
    public async Task WithHardmodeOffTheOwnersSessionShowsNoHardmode()
    {
        // Every account starts off (hardmode-leaderboard.md D30). The facts were captured all the
        // same, so the page strips them at render time: no skull flag, no Hardmode rank or gain, no
        // Hardmode strips, ladders or history tag - and a skull-only score is not a highlight.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 980993, false, ScoreEventClassification.NewPass) };

        var (_, model) = await BuildWith(chart, rows, mix: MixEnum.Phoenix2, highlights: HardmodeHighlights(chart),
            milestones: HardmodeMilestones());

        var score = Assert.Single(model.Hero!.Scores);
        Assert.False(score.IsFlagged);
        Assert.Null(score.Detail!.HardmodeRank);
        Assert.Null(score.Detail.HardmodeGain);
        Assert.DoesNotContain(model.Hero.Milestones, m => HardmodeVisibility.IsHardmode(m.Kind));
        Assert.Empty(model.Hero.HardmodeBars!);
        Assert.DoesNotContain(model.History.Single().Headline, m => HardmodeVisibility.IsHardmode(m.Kind));
    }

    [Fact]
    public async Task WithHardmodeOnTheSameSessionShowsItAll()
    {
        // The switch works backwards: the same captured rows, and the session fills back in.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 980993, false, ScoreEventClassification.NewPass) };

        var (_, model) = await BuildWith(chart, rows, mix: MixEnum.Phoenix2, highlights: HardmodeHighlights(chart),
            milestones: HardmodeMilestones(), hardmodeOn: true);

        var score = Assert.Single(model.Hero!.Scores);
        Assert.True(score.Flags.HasFlag(HighlightFlags.HardmodeTop50));
        Assert.Equal(1, score.Detail!.HardmodeRank);
        Assert.Contains(model.Hero.Milestones, m => m.Kind == MilestoneKind.HardmodePumbilityGain);
        Assert.NotEmpty(model.Hero.HardmodeBars!);
        Assert.Contains(model.History.Single().Headline, m => m.Kind == MilestoneKind.HardmodePumbilityGain);
    }

    [Fact]
    public async Task AFailedSettingsReadStillRendersTheSessionWithHardmodeOff()
    {
        // The switch only decides whether Hardmode rides on top: a transient failure reading it
        // costs the Hardmode, never the page.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 980993, false, ScoreEventClassification.NewPass) };

        var (_, model) = await BuildWith(chart, rows, mix: MixEnum.Phoenix2, highlights: HardmodeHighlights(chart),
            milestones: HardmodeMilestones(), settingsFail: true);

        var score = Assert.Single(model.Hero!.Scores);
        Assert.False(score.IsFlagged);
        Assert.DoesNotContain(model.Hero.Milestones, m => HardmodeVisibility.IsHardmode(m.Kind));
    }

    [Fact]
    public async Task ALinkToAnEarlierImportOfTheNightOpensTheWholeNight()
    {
        // Each import sends its own Discord card and links its own id; the night holds both.
        var chart = ChartAt(ChartType.Single, 21);
        var earlier = Guid.NewGuid();
        var night = Night(chart, earlier);
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { night }), chart);

        var model = await builder.Build(User, earlier, 1, 20, null, CancellationToken.None);

        Assert.Same(night, model.Hero!.Group);
        Assert.False(model.SelectedSessionWasUndone);
    }

    [Fact]
    public async Task ALinkOffThePageOnShowIsLookedUpRatherThanCalledUndone()
    {
        // A card from last week is several pages back. Searching only the cards on show is what
        // made an old link announce that its session had been undone.
        var chart = ChartAt(ChartType.Single, 21);
        var lastWeek = Guid.NewGuid();
        var linked = Group(new[]
        {
            Row(chart.Id, Start.AddDays(-7), 900000, false, ScoreEventClassification.NewPass) with
            {
                SessionId = lastWeek
            }
        }, lastWeek);
        var (mediator, builder) = Harness(new RecentSessionsPage(9, new[] { Night(chart, Guid.NewGuid()) }), chart,
            containing: linked);

        var model = await builder.Build(User, lastWeek, 1, 20, null, CancellationToken.None);

        Assert.Same(linked, model.Hero!.Group);
        Assert.False(model.SelectedSessionWasUndone);
        mediator.Verify(m => m.Send(It.Is<GetSessionContainingQuery>(q => q.UserId == User && q.SessionId == lastWeek),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ALinkNothingHoldsAnyMoreShowsTheNewestAndSaysItWasUndone()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var newest = Night(chart, Guid.NewGuid());
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { newest }), chart);

        var model = await builder.Build(User, Guid.NewGuid(), 1, 20, null, CancellationToken.None);

        Assert.Same(newest, model.Hero!.Group);
        Assert.True(model.SelectedSessionWasUndone);
    }

    [Fact]
    public async Task EveryImportInTheNightLoadsItsHighlightsAndMilestones()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var earlier = Guid.NewGuid();
        var (mediator, builder) = Harness(new RecentSessionsPage(1, new[] { Night(chart, earlier) }), chart);

        await builder.Build(User, null, 1, 20, null, CancellationToken.None);

        mediator.Verify(m => m.Send(
            It.Is<GetScoreHighlightsForSessionsQuery>(q =>
                q.SessionIds.Contains(earlier) && q.SessionIds.Contains(Session)),
            It.IsAny<CancellationToken>()), Times.Once);
        // Once for the hero, once for the history cards, both across the whole night.
        mediator.Verify(m => m.Send(
            It.Is<GetPlayerMilestonesForSessionsQuery>(q =>
                q.SessionIds.Contains(earlier) && q.SessionIds.Contains(Session)),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task APoolThatRoseInThreeImportsIsOneMovementOnTheHeroAndTheCard()
    {
        // Rendered as stored, the hero printed a PUMBILITY strip per import and the card a headline
        // per import, for one night's climb.
        var chart = ChartAt(ChartType.Single, 21);
        var earlier = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var night = Night(chart, earlier) with { SessionIds = new[] { earlier, middle, Session } };
        var milestones = new[]
        {
            new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, earlier, Start.AddMinutes(5), 8000, 8100, null,
                null),
            new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, middle, Start.AddMinutes(25), 8100, 8120, null,
                null),
            new PlayerMilestoneRecord(MilestoneKind.PumbilityGain, Session, Start.AddMinutes(45), 8120, 8150, null,
                null)
        };
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { night }), chart, milestones: milestones);

        var model = await builder.Build(User, null, 1, 20, null, CancellationToken.None);

        var strip = Assert.Single(model.Hero!.Milestones, m => m.Kind == MilestoneKind.PumbilityGain);
        Assert.Equal(8000, strip.OldValue);
        Assert.Equal(8150, strip.NewValue);
        Assert.Equal(8000, model.Hero.Ceremony.PumbilityOld);
        Assert.Equal(8150, model.Hero.Ceremony.PumbilityNew);
        var headline = Assert.Single(model.History.Single().Headline);
        Assert.Equal(8000, headline.OldValue);
        Assert.Equal(8150, headline.NewValue);
    }

    [Fact]
    public async Task TheNightStaysWatchableWhileItsNewestImportIsStillBeingCaptured()
    {
        // The earlier import captured long ago; the one that just landed is still inside the
        // window, and the page must keep listening for it.
        var chart = ChartAt(ChartType.Single, 21);
        var earlier = Guid.NewGuid();
        var sessions = new[]
        {
            Stored(earlier, "MURLOC#1", endedMinutesAgo: 60),
            Stored(Session, "MURLOC#1", endedMinutesAgo: 1)
        };
        var highlights = new[]
        {
            new ScoreHighlightRecord(chart.Id, earlier, Start, HighlightFlags.FolderDebut, 21, 21.0, null)
        };
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { Night(chart, earlier) }), chart, sessions,
            highlights);

        var model = await builder.Build(User, null, 1, 20, null, CancellationToken.None);

        Assert.True(model.Hero!.CaptureWindowOpen);
        Assert.False(model.Hero.CapturePending);
    }

    [Fact]
    public async Task PlaysCountsEveryPlayRatherThanTheRecordChanges()
    {
        // The session row counts what each batch changed: one new pass here. The night held three
        // plays, and "Plays" says three on the hero and on the card alike.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[]
        {
            Row(chart.Id, Start, 400000, true, ScoreEventClassification.Played),
            Row(chart.Id, Start.AddMinutes(4), 420000, true, ScoreEventClassification.Played),
            Row(chart.Id, Start.AddMinutes(8), 912400, false, ScoreEventClassification.NewPass)
        };
        var sessions = new[] { Stored(Session, null, endedMinutesAgo: 90, scoreCount: 1, newCount: 1) };
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { Group(rows, Session) }), chart, sessions);

        var model = await builder.Build(User, null, 1, 20, null, CancellationToken.None);

        Assert.Equal(3, model.Hero!.PlayCount);
        var card = model.History.Single();
        Assert.Equal(3, card.Plays);
        Assert.Equal(1, card.Passes);
    }

    [Fact]
    public async Task EveryCardTheNightPulledFromIsNamedOldestFirst()
    {
        // A second card in one night is the wrong-card case the line exists for.
        var chart = ChartAt(ChartType.Single, 21);
        var earlier = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var night = Night(chart, earlier) with { SessionIds = new[] { earlier, middle, Session } };
        var sessions = new[]
        {
            Stored(Session, "MURLOC#1", endedMinutesAgo: 30),
            Stored(earlier, "MURLOC#1", endedMinutesAgo: 120),
            Stored(middle, "ALT#2", endedMinutesAgo: 60)
        };
        var (_, builder) = Harness(new RecentSessionsPage(1, new[] { night }), chart, sessions);

        var model = await builder.Build(User, null, 1, 20, null, CancellationToken.None);

        Assert.Equal("MURLOC#1, ALT#2", model.Hero!.AccountTag);
        Assert.Equal("MURLOC#1, ALT#2", model.History.Single().AccountTag);
    }

    /// <summary>Two imports of one night: the pass in the earlier one, the upscore in the newest.</summary>
    private static RecentSessionsPage.SessionGroup Night(Chart chart, Guid earlierImport)
    {
        return Group(new[]
        {
            Row(chart.Id, Start.AddMinutes(40), 931000, false, ScoreEventClassification.Upscore),
            Row(chart.Id, Start, 905000, false, ScoreEventClassification.NewPass) with { SessionId = earlierImport }
        }, Session) with { SessionIds = new[] { earlierImport, Session } };
    }

    private static RecentSessionsPage.SessionGroup Group(RecentSessionsPage.ScoreEventRecord[] rows, Guid handle)
    {
        return new RecentSessionsPage.SessionGroup(handle, new[] { handle }, null, MixEnum.Phoenix,
            "officialImport", rows.Min(r => r.OccurredAt), rows.Max(r => r.OccurredAt), rows);
    }

    /// <summary>A ScoreSession row, its wall clock measured back from the harness's clock.</summary>
    private static ScoreSessionRecord Stored(Guid id, string? tag, int endedMinutesAgo, int scoreCount = 2,
        int newCount = 1)
    {
        var now = Start.AddHours(4);
        return new ScoreSessionRecord(id, User, MixEnum.Phoenix, "officialImport", tag, null,
            now.AddMinutes(-endedMinutesAgo - 5), now.AddMinutes(-endedMinutesAgo), scoreCount, newCount,
            scoreCount - newCount);
    }

    private static (Mock<IMediator> Mediator, SessionBreakdownBuilder Builder) Harness(RecentSessionsPage feed,
        Chart chart, IReadOnlyList<ScoreSessionRecord>? sessions = null, ScoreHighlightRecord[]? highlights = null,
        PlayerMilestoneRecord[]? milestones = null, RecentSessionsPage.SessionGroup? containing = null)
    {
        var mediator = new Mock<IMediator>();
        Setup(mediator, new GetRecentSessionsQuery(User, 1, 20), feed);
        Setup(mediator, new GetScoreSessionsQuery(User), sessions ?? Array.Empty<ScoreSessionRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetSessionContainingQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containing);
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        mediator.Setup(m => m.Send(It.IsAny<GetScoreHighlightsForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(highlights ?? Array.Empty<ScoreHighlightRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerMilestonesForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(milestones ?? Array.Empty<PlayerMilestoneRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPeerStandingsForScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ScoreOnChart, PeerStanding>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(User, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                22.6, 22.6, 23.4));

        var ledger = new Mock<IScoreReader>();
        ledger.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(Start.AddHours(4));
        return (mediator, new SessionBreakdownBuilder(mediator.Object, ledger.Object, clock.Object));
    }

    private static ScoreHighlightRecord[] HardmodeHighlights(Chart chart) => new[]
    {
        new ScoreHighlightRecord(chart.Id, Session, Start, HighlightFlags.HardmodeTop50, 21, 21.0,
            new HighlightDetail(HardmodeRank: 1, HardmodeGain: 354.24))
    };

    private static PlayerMilestoneRecord[] HardmodeMilestones() => new[]
    {
        new PlayerMilestoneRecord(MilestoneKind.HardmodePumbilityGain, Session, Start, 3339.22, 11131.72, null,
            "25|235")
    };

    private static async Task<SessionsPageModel> Build(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null, ScoreHighlightRecord[]? highlights = null,
        string source = "officialImport", int newCount = 1)
    {
        return (await BuildWith(chart, rows, mix, phoenix1, captured, sessionEndedMinutesAgo, highlights,
                source: source, newCount: newCount))
            .Model;
    }

    private static async Task<(SessionBreakdownBuilder Builder, SessionsPageModel Model)> BuildWith(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows,
        MixEnum mix = MixEnum.Phoenix, UserPhoenixScore[]? phoenix1 = null,
        bool captured = true, int? sessionEndedMinutesAgo = null, ScoreHighlightRecord[]? highlights = null,
        IReadOnlyDictionary<ScoreOnChart, PeerStanding>? standings = null,
        PlayerMilestoneRecord[]? milestones = null, bool hardmodeOn = false, bool settingsFail = false,
        string source = "officialImport", int newCount = 1)
    {
        var mediator = new Mock<IMediator>();
        if (hardmodeOn)
            mediator.Setup(m => m.Send(It.Is<GetUserUiSettingsQuery>(q => q.UserId == User),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [HardmodeOptIn.SettingKey] = "true"
                });
        if (settingsFail)
            mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("settings unavailable"));
        var group = new RecentSessionsPage.SessionGroup(Session, new[] { Session }, null, mix, "officialImport",
            rows.Min(r => r.OccurredAt), rows.Max(r => r.OccurredAt), rows);

        // Wall clock, deliberately distinct from the journal's play date: "is capture still
        // running" is a question about when the scores reached us.
        var now = Start.AddHours(4);
        var sessions = sessionEndedMinutesAgo == null
            ? Array.Empty<ScoreSessionRecord>()
            : new[]
            {
                new ScoreSessionRecord(Session, User, mix, source, "SHIRONEKO", "2",
                    now.AddMinutes(-sessionEndedMinutesAgo.Value - 5),
                    now.AddMinutes(-sessionEndedMinutesAgo.Value), rows.Length, newCount, 0)
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
            .ReturnsAsync(milestones ?? Array.Empty<PlayerMilestoneRecord>());
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
