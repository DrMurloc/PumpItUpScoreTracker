using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Xunit;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Events;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Tests.TestData;

namespace ScoreTracker.Tests.DomainTests;

[ExcludeFromCodeCoverage]
public sealed class PlayerHighlightPolicyTests
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

    private static PlayerMilestoneRecord PumbilityGain(double from, double to) =>
        new(MilestoneKind.PumbilityGain, SessionId: null, When, from, to, Title: null, Detail: null);

    private static PlayerMilestoneRecord FolderPassLamp(string folder) =>
        new(MilestoneKind.FolderPassLamp, SessionId: null, When, OldValue: null, NewValue: null, Title: null,
            Detail: folder);

    private static PlayerMilestoneRecord FolderProgress(string folder, int tier, PhoenixLetterGrade grade,
        int? fromTier = null, PhoenixLetterGrade? fromGrade = null) =>
        new(MilestoneKind.FolderProgress, SessionId: null, When, OldValue: null, NewValue: null, Title: null,
            new FolderProgressDetail(folder, tier, grade, fromTier, fromGrade).Format());

    private static Chart Chart(Guid id, int level, ChartType type = ChartType.Double, string song = "Bee") =>
        new ChartBuilder().WithId(id).WithLevel(level).WithType(type).WithSongName(song).Build();

    private static Dictionary<Guid, Chart> Charts(params Chart[] charts) => charts.ToDictionary(c => c.Id);

    private static RaritySnapshot Snapshot(
        (Guid ChartId, int PgHolders)? pg = null, int activePlayers = 1463)
    {
        var pgs = pg is { } p ? new Dictionary<Guid, int> { [p.ChartId] = p.PgHolders } : new Dictionary<Guid, int>();
        return new RaritySnapshot(pgs, activePlayers);
    }

    // Default player: zero competitive level, so the folder-debut gate never blocks unless a test
    // supplies one. Field order mirrors PlayerStatsRecord; only the competitive levels vary.
    private static PlayerStatsRecord Stats(double singles = 0, double doubles = 0, double overall = 0) =>
        new(Guid.NewGuid(), 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, overall, singles, doubles);

    private static IReadOnlyList<SignificantWin> Classify(ScoreHighlightsCapturedEvent e,
        IReadOnlyDictionary<Guid, Chart> charts, RaritySnapshot snapshot, PlayerStatsRecord? stats = null) =>
        PlayerHighlightPolicy.Classify(e, charts, snapshot, stats ?? Stats());

    [Fact]
    public void ADeepCompletionTierIsACommunityWin()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderProgress("S22", 80, PhoenixLetterGrade.AAPlus, fromTier: 60)),
            Charts(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderProgress, win.Kind);
        Assert.Equal("S22", win.Difficulty);
        Assert.Equal(80, win.Rank);
        // Detail is null when the tier is the news, so the row reads "80% of S22".
        Assert.Null(win.Detail);
    }

    [Fact]
    public void AShallowTierStaysOffTheFeedEvenThoughDiscordCarriesIt()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderProgress("S22", 40, PhoenixLetterGrade.AAPlus, fromTier: 20)),
            Charts(), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void AGradeClimbCountsOnlyFromSUpward()
    {
        var below = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderProgress("S22", 40, PhoenixLetterGrade.AAAPlus, fromGrade: PhoenixLetterGrade.AAA)),
            Charts(), Snapshot());
        var atS = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderProgress("S22", 40, PhoenixLetterGrade.S, fromGrade: PhoenixLetterGrade.AAAPlus)),
            Charts(), Snapshot());

        Assert.Empty(below);
        // Detail carries the grade when the grade is the news, so the row reads "S22 now S".
        Assert.Equal("S", Assert.Single(atS).Detail);
    }

    [Fact]
    public void ALampDoesNotDoubleUpAsProgressAndACompletion()
    {
        // FolderPassLamp and FolderProgress both fire at 100% — the feed shows one row.
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                FolderPassLamp("S24"),
                FolderProgress("S24", 100, PhoenixLetterGrade.APlus, fromTier: 80)),
            Charts(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderComplete, win.Kind);
    }

    [Fact]
    public void DifficultyTitleCompletionIsABigTitle()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted(DifficultyTitle)),
            new Dictionary<Guid, Chart>(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.BigTitle, win.Kind);
        Assert.Equal(DifficultyTitle, win.TitleName);
    }

    [Fact]
    public void AnyEarnedTitleIsAWinWithNoRarityClaim()
    {
        // "All titles are big titles" (owner, 2026-08-14) — no rarity gate, no population read.
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("SCROOGE")),
            new Dictionary<Guid, Chart>(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.BigTitle, win.Kind);
        Assert.Equal("SCROOGE", win.TitleName);
        Assert.Null(win.RarityShare);
    }

    [Fact]
    public void TheDefaultTitleNeverAnnounces()
    {
        // A first import "earns" the account's default title; that is noise, not news.
        var phoenix = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("Beginner")),
            new Dictionary<Guid, Chart>(), Snapshot());
        var phoenix2 = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("BEGINNER")),
            new Dictionary<Guid, Chart>(), Snapshot());

        Assert.Empty(phoenix);
        Assert.Empty(phoenix2);
    }

    [Fact]
    public void APgFewerThanOnePercentHoldOnAHardChartIsNotable()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
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
        var wins = Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId, plate: "Perfect Game") }),
            Charts(Chart(chartId, 19)), Snapshot(pg: (chartId, 1), activePlayers: 1463));

        Assert.Empty(wins);
    }

    [Fact]
    public void ACommonPgIsNotNotable()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId, plate: "Perfect Game") }),
            Charts(Chart(chartId, 24)), Snapshot(pg: (chartId, 100), activePlayers: 1463));

        Assert.Empty(wins);
    }

    [Fact]
    public void ATopTenPumbilityScoreIsAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
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
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.PumbilityTop50, new HighlightDetail(PumbilityRank: 11)) }),
            Charts(Chart(chartId, 26)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void TheBestScoreAmongPeersIsPeerEliteAtPositionOne()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
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
        var wins = Classify(
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
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 20, PeerBetterCount: 2)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void ASmallCohortIsNotPeerElite()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.ScoreQuality90, new HighlightDetail(PeerCount: 5, PeerBetterCount: 0)) }),
            Charts(Chart(chartId, 25)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void OneOfTheFirstThreePassesInAFolderIsAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
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
        var wins = Classify(
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
        var wins = Classify(
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
        var wins = Classify(
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
        var wins = Classify(
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
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("Expert Lv. 1"), TitleCompleted("Expert Lv. 2"), TitleCompleted("Expert Lv. 3"),
                TitleCompleted("Expert Lv. 4"), TitleCompleted("Expert Lv. 5")),
            new Dictionary<Guid, Chart>(), Snapshot());

        Assert.Equal(PlayerHighlightPolicy.MaxWinsPerEvent, wins.Count);
    }

    [Fact]
    public void ABatchWithNoBigWinsYieldsNothing()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix, new[] { Change(chartId) }),
            Charts(Chart(chartId, 18)), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void AFolderDebutBelowFlooredCompetitiveLevelIsNotAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.FolderDebut, new HighlightDetail(FolderDebutOrdinal: 1)) }),
            Charts(Chart(chartId, 20, ChartType.Double)), Snapshot(),
            Stats(doubles: 24.5));

        Assert.Empty(wins);
    }

    [Fact]
    public void AFolderDebutAtTheFlooredCompetitiveLevelIsAWin()
    {
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.FolderDebut, new HighlightDetail(FolderDebutOrdinal: 2)) }),
            Charts(Chart(chartId, 24, ChartType.Double)), Snapshot(),
            Stats(doubles: 24.5));

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderFirst, win.Kind);
        Assert.Equal(2, win.Rank);
    }

    [Fact]
    public void AFolderDebutIsGatedByTheCompetitiveLevelForItsOwnType()
    {
        // A Singles folder gates on Singles competitive level, not the higher Doubles one.
        var chartId = Guid.NewGuid();
        var wins = Classify(
            Event(MixEnum.Phoenix,
                new[] { Change(chartId, HighlightFlags.FolderDebut, new HighlightDetail(FolderDebutOrdinal: 1)) }),
            Charts(Chart(chartId, 20, ChartType.Single)), Snapshot(),
            Stats(singles: 18, doubles: 26));

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.FolderFirst, win.Kind);
    }

    // ---- pumbility ladder roll-up (owner, 2026-08-14; feeds only, the card stays loud) ----

    [Fact]
    public void SeveralRungsOfOnePumbilityLadderRollIntoOneSpan()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("[S] ADVANCED LV.7"), TitleCompleted("[S] ADVANCED LV.6"),
                TitleCompleted("[S] ADVANCED LV.9"), TitleCompleted("[S] ADVANCED LV.8")),
            Charts(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.PumbilityTitleSpan, win.Kind);
        Assert.Equal("[S] ADVANCED LV.9", win.TitleName); // the rung reached
        Assert.Equal("[S] ADVANCED LV.6", win.Detail);    // the first rung crossed
    }

    [Fact]
    public void EachPumbilityPoolRollsUpSeparately()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("[S] ADVANCED LV.6"), TitleCompleted("[S] ADVANCED LV.7"),
                TitleCompleted("[D] INTERMEDIATE LV.3"), TitleCompleted("[D] INTERMEDIATE LV.4")),
            Charts(), Snapshot());

        Assert.Equal(2, wins.Count);
        Assert.All(wins, w => Assert.Equal(WinKind.PumbilityTitleSpan, w.Kind));
        Assert.Contains(wins, w => w.TitleName == "[S] ADVANCED LV.7" && w.Detail == "[S] ADVANCED LV.6");
        Assert.Contains(wins, w => w.TitleName == "[D] INTERMEDIATE LV.4" && w.Detail == "[D] INTERMEDIATE LV.3");
    }

    [Fact]
    public void ALonePumbilityRungStaysAPlainTitleRow()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("[S] ADVANCED LV.6")),
            Charts(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.BigTitle, win.Kind);
        Assert.Equal("[S] ADVANCED LV.6", win.TitleName);
    }

    [Fact]
    public void PhoenixDifficultyChainsDoNotRollUp()
    {
        // Only the pumbility pool ladders roll (owner: "specifically only the pumbility titles") —
        // a Phoenix difficulty chain prints one row per rung.
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                TitleCompleted("Expert Lv. 1"), TitleCompleted("Expert Lv. 2")),
            new Dictionary<Guid, Chart>(), Snapshot());

        Assert.Equal(2, wins.Count);
        Assert.All(wins, w => Assert.Equal(WinKind.BigTitle, w.Kind));
    }

    // ---- the PUMBILITY level crossing (docs/design/pumbility-levels.md §5) ----

    [Fact]
    public void ALevelCrossingWithoutItsGemTitleIsAWin()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                PumbilityGain(17_410.38, 17_602.69)),
            Charts(), Snapshot());

        var win = Assert.Single(wins);
        Assert.Equal(WinKind.PumbilityLevelUp, win.Kind);
        // Rank carries the badge index reached — DIAMOND LV.4 — and PoolValue the raw pool.
        Assert.Equal(24, win.Rank);
        Assert.Equal(17_602.69, win.PoolValue);
    }

    [Fact]
    public void AGainInsideOneRungSaysNothing()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                PumbilityGain(17_610, 17_650)),
            Charts(), Snapshot());

        Assert.Empty(wins);
    }

    [Fact]
    public void AGemTitleInTheSameBatchOutranksTheLevelRow()
    {
        // Crossing into RED BERYL LV.1 IS the [P.B] RED BERYL title; when the batch completed it,
        // the title is the sentence and the level row stands down. This is the owner's "didn't
        // change titles but changed levels", stated from the other side.
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                PumbilityGain(17_950, 18_010), TitleCompleted("[P.B] RED BERYL")),
            Charts(), Snapshot());

        Assert.DoesNotContain(wins, w => w.Kind == WinKind.PumbilityLevelUp);
        Assert.Contains(wins, w => w.Kind == WinKind.BigTitle && w.TitleName == "[P.B] RED BERYL");
    }

    [Fact]
    public void ASinglesLadderTitleNeverSuppressesTheLevel()
    {
        // The [S]/[D] ladders have no levels — completing one says nothing about the gem ladder.
        var wins = Classify(
            Event(MixEnum.Phoenix2, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                PumbilityGain(17_410.38, 17_602.69), TitleCompleted("[S] ADVANCED LV.2")),
            Charts(), Snapshot());

        Assert.Contains(wins, w => w.Kind == WinKind.PumbilityLevelUp);
    }

    [Fact]
    public void PhoenixHasNoGemLadderAndMintsNoLevel()
    {
        var wins = Classify(
            Event(MixEnum.Phoenix, Array.Empty<ScoreHighlightsCapturedEvent.HighlightedChange>(),
                PumbilityGain(17_410.38, 17_602.69)),
            Charts(), Snapshot());

        Assert.Empty(wins);
    }
}
