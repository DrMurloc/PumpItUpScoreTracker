using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Domain;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;

namespace ScoreTracker.Tests.DomainTests;

[ExcludeFromCodeCoverage]
public sealed class CommunityHighlightPolicyTests
{
    private static readonly DateTimeOffset When = new(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);

    // "Expert Lv. 4" is a shipped Phoenix difficulty title (Category "Difficulty").
    private const string DifficultyTitle = "Expert Lv. 4";

    private static ScoreHighlightsCapturedEvent Event(MixEnum mix,
        IReadOnlyList<ScoreHighlightsCapturedEvent.HighlightedChange> changes,
        params PlayerMilestoneRecord[] milestones) =>
        ScoreHighlightsCapturedEvent.Create(When, Guid.NewGuid(), mix, sessionId: null, changes, milestones);

    private static ScoreHighlightsCapturedEvent.HighlightedChange Change(Guid chartId,
        HighlightFlags flags = HighlightFlags.None, HighlightDetail? detail = null,
        string? plate = null, bool isBroken = false, int? newScore = null) =>
        new(chartId, IsNewPass: true, OldScore: null, NewScore: newScore, plate, isBroken, flags, detail);

    private static PlayerMilestoneRecord TitleCompleted(string title) =>
        new(MilestoneKind.TitleCompleted, SessionId: null, When, OldValue: null, NewValue: null, title, Detail: null);

    private static PlayerMilestoneRecord FolderPassLamp(string folder) =>
        new(MilestoneKind.FolderPassLamp, SessionId: null, When, OldValue: null, NewValue: null, Title: null,
            Detail: folder);

    private static Chart Chart(Guid id, int level, ChartType type = ChartType.Double, string song = "Bee") =>
        new ChartBuilder().WithId(id).WithLevel(level).WithType(type).WithSongName(song).Build();

    private static Dictionary<Guid, Chart> Charts(params Chart[] charts) => charts.ToDictionary(c => c.Id);

    private static RaritySnapshot Snapshot(
        (Guid ChartId, int PgHolders)? pg = null, int activePlayers = 1463,
        (string Title, int Holders)? title = null, int titledUsers = 1000)
    {
        var pgs = pg is { } p ? new Dictionary<Guid, int> { [p.ChartId] = p.PgHolders } : new Dictionary<Guid, int>();
        var titles = title is { } t ? new Dictionary<string, int> { [t.Title] = t.Holders } : new Dictionary<string, int>();
        return new RaritySnapshot(pgs, activePlayers, titles, titledUsers);
    }

    [Fact]
    public void DifficultyTitleCompletionIsABigTitle()
    {
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted(DifficultyTitle)),
            new Dictionary<Guid, Chart>(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.BigTitle, win.Kind);
        Assert.Equal(DifficultyTitle, win.TitleName);
    }

    [Fact]
    public void ATitleHeldByUnderOnePercentOfTitledPlayersIsARareTitle()
    {
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("SCROOGE")),
            new Dictionary<Guid, Chart>(), Snapshot(title: ("SCROOGE", 5), titledUsers: 1000));

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.RareTitle, win.Kind);
        Assert.Equal(0.005, win.RarityShare);
    }

    [Fact]
    public void ACommonTitleIsNotAWin()
    {
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("SCROOGE")),
            new Dictionary<Guid, Chart>(), Snapshot(title: ("SCROOGE", 500), titledUsers: 1000));

        Assert.Empty(wins);
    }

    [Fact]
    public void APgFewerThanOnePercentHoldOnAHardChartIsNotable()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId, plate: "Perfect Game") }),
            Charts(Chart(chartId, 24)), Snapshot(pg: (chartId, 5), activePlayers: 1463));

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.NotablePg, win.Kind);
        Assert.Equal(chartId, win.ChartId);
        Assert.True(win.RarityShare < 0.01);
    }

    [Fact]
    public void APgBelowTheLevelFloorIsNotNotable()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId, plate: "Perfect Game") }),
            Charts(Chart(chartId, 19)), Snapshot(pg: (chartId, 1), activePlayers: 1463));

        Assert.Empty(wins);
    }

    [Fact]
    public void ACommonPgIsNotNotable()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId, plate: "Perfect Game") }),
            Charts(Chart(chartId, 24)), Snapshot(pg: (chartId, 100), activePlayers: 1463));

        Assert.Empty(wins);
    }

    [Fact]
    public void ATopTenPumbilityScoreIsAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.PumbilityTop50, new HighlightDetail(PumbilityRank: 10)) }),
            Charts(Chart(chartId, 26)), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.TopPumbility, win.Kind);
        Assert.Equal(10, win.Rank);
    }

    [Fact]
    public void APumbilityRankPastTenIsNotAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.PumbilityTop50, new HighlightDetail(PumbilityRank: 11)) }),
            Charts(Chart(chartId, 26)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void TheBestScoreAmongPeersIsPeerEliteAtPositionOne()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 20, PeerBetterCount: 0)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.PeerElite, win.Kind);
        Assert.Equal(1, win.Rank); // #1 — nobody beat you; the widget renders "#1 of all peers"
    }

    [Fact]
    public void ATopFivePercentButNotFirstScoreCarriesItsPositionAndFraction()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 100, PeerBetterCount: 3)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.PeerElite, win.Kind);
        Assert.Equal(4, win.Rank);           // 3 beat you → position #4
        Assert.Equal(0.04, win.RarityShare); // 4/100 → widget shows "top 4%"
    }

    [Fact]
    public void AScoreOutsideTheTopFivePercentIsNotPeerElite()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 20, PeerBetterCount: 2)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void ASmallCohortIsNotPeerElite()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 5, PeerBetterCount: 0)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void OneOfTheFirstThreePassesInAFolderIsAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.FolderDebut, new HighlightDetail(FolderDebutOrdinal: 3)) }),
            Charts(Chart(chartId, 23)), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderFirst, win.Kind);
        Assert.Equal(3, win.Rank);
    }

    [Fact]
    public void TheFourthPassInAFolderIsNotAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.FolderDebut, new HighlightDetail(FolderDebutOrdinal: 4)) }),
            Charts(Chart(chartId, 23)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void APerfectGameIsNeverAlsoCountedAsPeerElite()
    {
        // A PG flagged ScoreQuality90 but common sitewide must produce NO win (not a peer-elite line).
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, new[]
            {
                Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 20, PeerBetterCount: 0),
                    plate: "Perfect Game")
            }),
            Charts(Chart(chartId, 24)), Snapshot(pg: (chartId, 100), activePlayers: 1463));

        Assert.Empty(wins);
    }

    [Fact]
    public void AFullFolderClearIsAFolderCompleteWin()
    {
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderPassLamp("D23")),
            new Dictionary<Guid, Chart>(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderComplete, win.Kind);
        Assert.Equal("D23", win.Difficulty);
    }

    [Fact]
    public void AChartWinCarriesTheScore()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix,
                new[]
                {
                    Change(chartId, HighlightFlags.PumbilityTop50, new HighlightDetail(PumbilityRank: 3),
                        newScore: 998_000)
                }),
            Charts(Chart(chartId, 26)), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.TopPumbility, win.Kind);
        Assert.Equal(998_000, win.Score);
    }

    [Fact]
    public void TheSummaryIsCappedAtFourWins()
    {
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("Expert Lv. 1"), TitleCompleted("Expert Lv. 2"), TitleCompleted("Expert Lv. 3"),
                TitleCompleted("Expert Lv. 4"), TitleCompleted("Expert Lv. 5")),
            new Dictionary<Guid, Chart>(), Snapshot());

        Assert.Equal(CommunityHighlightPolicy.MaxWinsPerEvent, wins.Count);
    }

    [Fact]
    public void ABatchWithNoBigWinsYieldsNothing()
    {
        var chartId = Guid.NewGuid();
        var wins = CommunityHighlightPolicy.Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId) }),
            Charts(Chart(chartId, 18)), Snapshot());

        Assert.Empty(wins);
    }
}
