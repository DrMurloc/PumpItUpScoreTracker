using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class HighlightsCalculatorTests
{
    private const int SnapshotId = 9;
    private const int PumbilityBoardId = 1;
    private const int ChartBoardId = 10;
    private const int SecondChartBoardId = 11;

    private static readonly BoardDimension Pumbility =
        new(PumbilityBoardId, LeaderboardTypes.Rating, "PUMBILITY", null, null, null);

    private static BoardDimension ChartBoard(int id = ChartBoardId, int level = 26, string type = "Double",
        string name = "Kugutsu D26")
    {
        return new BoardDimension(id, LeaderboardTypes.Chart, name, Guid.NewGuid(), type, level);
    }

    private static HighlightsInput Input(
        IEnumerable<BoardDimension>? boards = null,
        IEnumerable<PlacementRow>? current = null,
        IEnumerable<PlacementRow>? previous = null,
        IEnumerable<BoardRecordRow>? boardRecords = null,
        IEnumerable<FolderRecordRow>? folderRecords = null,
        bool isBaseline = false,
        CrossMixRecordHighs? crossMix = null,
        IReadOnlySet<int>? seen = null,
        ScoringConfiguration? scoring = null,
        MixEnum mix = MixEnum.Phoenix2)
    {
        return new HighlightsInput(mix, SnapshotId, isBaseline,
            (boards ?? new[] { Pumbility }).ToArray(),
            (current ?? Array.Empty<PlacementRow>()).ToArray(),
            previous?.ToArray(),
            (boardRecords ?? Array.Empty<BoardRecordRow>()).ToArray(),
            (folderRecords ?? Array.Empty<FolderRecordRow>()).ToArray(),
            crossMix, seen, scoring);
    }

    [Fact]
    public void TheWeeklyPulseCountsCausedMovementOnly()
    {
        // Player 1 is new to the board, 2 upscored, 3 merely got pushed down a place,
        // and 4 dropped off — only 1 and 2 caused movement.
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[]
            {
                new PlacementRow(board.Id, 1, 1, 970000),
                new PlacementRow(board.Id, 2, 2, 965000),
                new PlacementRow(board.Id, 3, 3, 960000)
            },
            previous: new[]
            {
                new PlacementRow(board.Id, 2, 1, 950000),
                new PlacementRow(board.Id, 3, 2, 960000),
                new PlacementRow(board.Id, 4, 3, 940000)
            },
            crossMix: CrossMixRecordHighs.Empty,
            seen: new HashSet<int> { 1, 2, 3, 4 }));

        var pulse = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.WeeklyPulse);
        Assert.Null(pulse.PlayerId);
        Assert.Equal(1, pulse.PrevValue);
        Assert.Equal(1, pulse.NewValue);
        Assert.Equal(2, pulse.Score);
        Assert.Equal(0, pulse.Level);
    }

    [Fact]
    public void DebutsAreFirstEverChartBoardAppearancesOrderedByBestPlace()
    {
        // Players 7 and 9 have never been on any board; 9's best place is better so they
        // lead. Player 5 is new to the PUMBILITY board only — a rating row is no debut.
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { Pumbility, board },
            current: new[]
            {
                new PlacementRow(board.Id, 1, 1, 970000),
                new PlacementRow(board.Id, 9, 2, 965000),
                new PlacementRow(board.Id, 7, 5, 950000),
                new PlacementRow(PumbilityBoardId, 5, 40, 8000m)
            },
            previous: new[] { new PlacementRow(board.Id, 1, 1, 969000) },
            crossMix: CrossMixRecordHighs.Empty,
            seen: new HashSet<int> { 1 }));

        var debuts = result.Highlights.Where(h => h.Kind == HighlightKinds.Debut).ToArray();
        Assert.Equal(new int?[] { 9, 7 }, debuts.Select(d => d.PlayerId).ToArray());
        Assert.Equal(new[] { 1, 2 }, debuts.Select(d => d.SortOrder).ToArray());
        Assert.Equal(2, Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.WeeklyPulse).Level);
    }

    [Fact]
    public void EveryDebutGetsAStoredRowNoMatterHowMany()
    {
        // A first-real-week class of forty rookies stores forty rows — the strip expands
        // to the whole class, so nothing gets sampled away.
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: Enumerable.Range(1, 40)
                .Select(i => new PlacementRow(board.Id, 100 + i, i, 990000 - i * 100))
                .ToArray(),
            previous: Array.Empty<PlacementRow>(),
            crossMix: CrossMixRecordHighs.Empty,
            seen: new HashSet<int>()));

        var debuts = result.Highlights.Where(h => h.Kind == HighlightKinds.Debut).ToArray();
        Assert.Equal(40, debuts.Length);
        Assert.Equal(Enumerable.Range(1, 40), debuts.Select(d => d.SortOrder));
        Assert.Equal(40, Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.WeeklyPulse).Level);
    }

    [Fact]
    public void GainersRankByPumbilityValueGained()
    {
        var result = HighlightsCalculator.Calculate(Input(
            current: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 1, 18100.00m),
                new PlacementRow(PumbilityBoardId, 2, 2, 18050.50m),
                new PlacementRow(PumbilityBoardId, 3, 3, 17000.00m)
            },
            previous: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 2, 18000.00m),
                new PlacementRow(PumbilityBoardId, 2, 1, 17800.25m),
                new PlacementRow(PumbilityBoardId, 3, 3, 17005.00m)
            }));

        var gainers = result.Highlights.Where(h => h.Kind == HighlightKinds.PumbilityGainer).ToArray();
        Assert.Equal(new int?[] { 2, 1 }, gainers.Select(g => g.PlayerId).ToArray());
        Assert.Equal(18050.50m, gainers[0].Score);
        Assert.Equal(17800.25m, gainers[0].PrevValue);
        Assert.Equal(2, gainers[0].NewValue);
        Assert.Equal(1, gainers[0].Level);
    }

    [Fact]
    public void FloorMarksCarryBothWeeksValuesAndFiftySsLevels()
    {
        // A 1,200-deep board: each landmark rank stores its floor, last week's floor, and
        // the 50x SS level equivalents on both sides.
        var scoring = ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false);
        var current = Enumerable.Range(1, 1200)
            .Select(place => new PlacementRow(PumbilityBoardId, place + 10000, place, 12000m - place * 5))
            .ToArray();
        var previous = Enumerable.Range(1, 1200)
            .Select(place => new PlacementRow(PumbilityBoardId, place + 10000, place, 11500m - place * 5))
            .ToArray();
        var result = HighlightsCalculator.Calculate(Input(
            current: current,
            previous: previous,
            scoring: scoring));

        var floors = result.Highlights.Where(h => h.Kind == HighlightKinds.FloorMark)
            .OrderBy(h => h.SortOrder).ToArray();
        Assert.Equal(new[] { 100, 1000 }, floors.Select(f => f.SortOrder).ToArray());
        Assert.Equal(11500m, floors[0].Score);
        Assert.Equal(11000m, floors[0].PrevValue);
        Assert.Equal(7000m, floors[1].Score);
        // Levels mirror the What It Takes cutline: uniform 50x SS, Singles, SG plates.
        Assert.Equal(CutlineCalculator.LevelFor(scoring, ChartType.Single, PhoenixLetterGrade.SS, 11500m),
            floors[0].Level);
        Assert.Equal(CutlineCalculator.LevelFor(scoring, ChartType.Single, PhoenixLetterGrade.SS, 7000m),
            floors[1].Level);
    }

    [Fact]
    public void BaselineEmitsNoHeroRows()
    {
        var result = HighlightsCalculator.Calculate(Input(
            current: new[] { new PlacementRow(PumbilityBoardId, 1, 1, 18000m) },
            isBaseline: true,
            seen: new HashSet<int>(),
            scoring: ScoringConfiguration.PumbilityScoring(MixEnum.Phoenix2, false)));

        Assert.Empty(result.Highlights);
    }

    [Fact]
    public void BandAlreadyClaimedInAnotherMixDowngradesTheFirstToANewNumberOne()
    {
        // The chart carried an SS on another mix's boards, so this mix's first SS is a
        // reclear: it still beat the standing record here, so it lands as a new #1.
        var board = ChartBoard(level: 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 981500) },
            previous: new[] { new PlacementRow(board.Id, 9, 1, 962000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 962000, 1) },
            crossMix: new CrossMixRecordHighs(
                new Dictionary<Guid, int>
                {
                    [board.ChartId!.Value] = GradeBandLadder.Of(983000, MixEnum.Phoenix).Rank
                },
                new Dictionary<(string, int), int>())));

        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        var numberOne = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.NewNumberOne);
        Assert.Equal(7, numberOne.PlayerId);
    }

    [Fact]
    public void ABandAboveTheCrossMixHighStillFiresAsAWorldFirst()
    {
        var board = ChartBoard(level: 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 991000) },
            previous: new[] { new PlacementRow(board.Id, 9, 1, 962000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 962000, 1) },
            crossMix: new CrossMixRecordHighs(
                new Dictionary<Guid, int>
                {
                    [board.ChartId!.Value] = GradeBandLadder.Of(983000, MixEnum.Phoenix).Rank
                },
                new Dictionary<(string, int), int>())));

        var first = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.Equal("SSS", first.GradeBand);
    }

    [Fact]
    public void FolderFirstsRespectTheOtherMixesFolderRecordAndFallThroughToTheChart()
    {
        // Another mix's D26 folder already held an SS, so no folder banner — but THIS chart
        // never had one anywhere, so the chart-level world first still fires.
        var board = ChartBoard(level: 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 981500) },
            previous: new[] { new PlacementRow(board.Id, 9, 1, 962000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 962000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 962000, 1) },
            crossMix: new CrossMixRecordHighs(
                new Dictionary<Guid, int>(),
                new Dictionary<(string, int), int>
                {
                    [("Double", 26)] = GradeBandLadder.Of(984000, MixEnum.Phoenix).Rank
                })));

        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.FolderGradeFirst);
        Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
    }

    [Fact]
    public void MoversRankByPumbilityRankImprovement()
    {
        var result = HighlightsCalculator.Calculate(Input(
            current: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 17, 18204.51m),
                new PlacementRow(PumbilityBoardId, 2, 18, 18101.09m),
                new PlacementRow(PumbilityBoardId, 3, 47, 17411.87m)
            },
            previous: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 31, 17892.11m),
                new PlacementRow(PumbilityBoardId, 2, 27, 17872.93m),
                new PlacementRow(PumbilityBoardId, 3, 54, 17221.85m)
            }));

        var movers = result.Highlights.Where(h => h.Kind == HighlightKinds.PumbilityMover).ToArray();
        Assert.Equal(3, movers.Length);
        Assert.Equal(1, movers[0].PlayerId);
        Assert.Equal(1, movers[0].SortOrder);
        Assert.Equal(31, movers[0].PrevValue);
        Assert.Equal(17, movers[0].NewValue);
        Assert.Equal(18204.51m, movers[0].Score);
        Assert.Equal(new int?[] { 1, 2, 3 }, movers.Select(m => m.PlayerId).ToArray());
    }

    [Fact]
    public void MoversNeedBothWeeksAndAPositiveDelta()
    {
        var result = HighlightsCalculator.Calculate(Input(
            current: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 5, 18000m), // fell from 3
                new PlacementRow(PumbilityBoardId, 2, 9, 17500m) // new to the board
            },
            previous: new[]
            {
                new PlacementRow(PumbilityBoardId, 1, 3, 18100m)
            }));

        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.PumbilityMover);
    }

    [Fact]
    public void MixWithoutAPumbilityBoardGetsNoMovers()
    {
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 1, 1, 950000) },
            previous: new[] { new PlacementRow(board.Id, 1, 2, 940000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 960000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 960000, 1) }));

        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.PumbilityMover);
    }

    [Fact]
    public void BoardsClimbedCountsImprovementsAndNewEntries()
    {
        var boards = Enumerable.Range(100, 6).Select(id => ChartBoard(id, name: $"Board {id}")).ToArray();
        // Player 1 climbs 4 boards and enters a 5th fresh; player 2 climbs one board one place.
        var current = boards.Take(4)
            .Select(b => new PlacementRow(b.Id, 1, 5, 900000))
            .Append(new PlacementRow(boards[4].Id, 1, 20, 880000))
            .Append(new PlacementRow(boards[5].Id, 2, 3, 910000))
            .ToArray();
        var previous = boards.Take(4)
            .Select(b => new PlacementRow(b.Id, 1, 15, 880000))
            .Append(new PlacementRow(boards[5].Id, 2, 4, 905000))
            .ToArray();
        var records = boards.Select(b => new BoardRecordRow(b.Id, 999000, 1));

        var result = HighlightsCalculator.Calculate(Input(
            boards: boards, current: current, previous: previous, boardRecords: records,
            folderRecords: new[] { new FolderRecordRow("Double", 26, 999000, 1) }));

        // No eligibility gate: every climber lists, net-first — even a one-board move.
        var climbed = result.Highlights.Where(h => h.Kind == HighlightKinds.BoardsClimbed)
            .OrderBy(h => h.SortOrder).ToArray();
        Assert.Equal(new int?[] { 1, 2 }, climbed.Select(c => c.PlayerId).ToArray());
        var playerOne = climbed[0];
        Assert.Equal(5, playerOne.NewValue); // four climbs + one new entry
        // Four boards climbed 15 -> 5 (+40) plus the fresh entry's climb from off the
        // board (its one-row board floors the credit at 1).
        Assert.Equal(41, playerOne.PrevValue);
        Assert.Equal(1, playerOne.Level); // the split remembers which were first-time entries
        Assert.Equal(1, climbed[1].PrevValue);
    }

    [Fact]
    public void ClimbersRankByNetPlacesNotBoardCount()
    {
        // Player 1 tiptoes up six boards (+1 each); player 2 climbs five boards hard
        // (+20 each). Net leads, so depth beats breadth.
        var boards = Enumerable.Range(200, 6).Select(id => ChartBoard(id, name: $"Board {id}")).ToArray();
        var current = boards.Select(b => new PlacementRow(b.Id, 1, 9, 900000))
            .Concat(boards.Take(5).Select(b => new PlacementRow(b.Id, 2, 30, 890000)))
            .ToArray();
        var previous = boards.Select(b => new PlacementRow(b.Id, 1, 10, 899000))
            .Concat(boards.Take(5).Select(b => new PlacementRow(b.Id, 2, 50, 880000)))
            .ToArray();
        var result = HighlightsCalculator.Calculate(Input(
            boards: boards, current: current, previous: previous,
            boardRecords: boards.Select(b => new BoardRecordRow(b.Id, 999000, 1)).ToArray()));

        var climbed = result.Highlights.Where(h => h.Kind == HighlightKinds.BoardsClimbed)
            .OrderBy(h => h.SortOrder).ToArray();
        Assert.Equal(new int?[] { 2, 1 }, climbed.Select(c => c.PlayerId).ToArray());
        Assert.Equal(100, climbed[0].PrevValue);
        Assert.Equal(6, climbed[1].PrevValue);
    }

    [Fact]
    public void EnteringABoardCreditsTheClimbFromOffTheBoard()
    {
        // Fifty players hold the board; the newcomer lands #1 and is credited all fifty —
        // one deep splash is enough to list.
        var board = ChartBoard();
        var current = Enumerable.Range(2, 49)
            .Select(place => new PlacementRow(board.Id, place + 100, place, 990000 - place * 100))
            .Append(new PlacementRow(board.Id, 7, 1, 991000))
            .ToArray();
        var previous = Enumerable.Range(2, 49)
            .Select(place => new PlacementRow(board.Id, place + 100, place - 1, 990000 - place * 100))
            .ToArray();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: current,
            previous: previous,
            boardRecords: new[] { new BoardRecordRow(board.Id, 999000, 1) }));

        var entry = Assert.Single(result.Highlights, h =>
            h.Kind == HighlightKinds.BoardsClimbed && h.PlayerId == 7);
        Assert.Equal(1, entry.NewValue); // one board
        Assert.Equal(50, entry.PrevValue); // #1 over fifty players
        Assert.Equal(1, entry.Level);
    }

    [Fact]
    public void SubSsBandsAreWorldFirstsToo()
    {
        // First AAA on a hard chart is huge: the standing Phoenix 2 record was an AA+
        // (948k), the Phoenix board also only ever reached AA+, and this week's 952k
        // crosses into AAA for the first time anywhere.
        var board = ChartBoard(level: 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 952000) },
            previous: new[] { new PlacementRow(board.Id, 9, 1, 948000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 948000, 1) },
            crossMix: new CrossMixRecordHighs(
                new Dictionary<Guid, int>
                {
                    [board.ChartId!.Value] = GradeBandLadder.Of(945000, MixEnum.Phoenix).Rank
                },
                new Dictionary<(string, int), int>())));

        var first = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.Equal("AAA", first.GradeBand);
    }

    [Fact]
    public void TyingThePerfectCeilingNeverListsAsANewNumberOne()
    {
        // The chart's PG lives on the other mix, so this week's PG is no world first — and
        // matching a perfect dethrones nobody, so it is no new #1 either.
        var board = ChartBoard(level: 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 1_000_000) },
            previous: new[] { new PlacementRow(board.Id, 9, 1, 998_000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 998_000, 1) },
            crossMix: new CrossMixRecordHighs(
                new Dictionary<Guid, int>
                {
                    [board.ChartId!.Value] = GradeBandLadder.Of(1_000_000, MixEnum.Phoenix).Rank
                },
                new Dictionary<(string, int), int>())));

        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.NewNumberOne);
        // The record book still advances to the perfect.
        Assert.Equal(1_000_000, result.UpdatedBoardRecords.Single().HighScore);
    }

    [Fact]
    public void NewNumberOneRequiresBeatingTheStandingRecordNotMatchingIt()
    {
        var board = ChartBoard();
        var baseInput = Input(
            boards: new[] { board },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 970000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 970000, 1) });

        var matched = HighlightsCalculator.Calculate(baseInput with
        {
            Current = new[] { new PlacementRow(board.Id, 7, 1, 970000) }
        });
        var beaten = HighlightsCalculator.Calculate(baseInput with
        {
            Current = new[] { new PlacementRow(board.Id, 7, 1, 971500) }
        });

        Assert.DoesNotContain(matched.Highlights, h => h.Kind == HighlightKinds.NewNumberOne);
        var highlight = Assert.Single(beaten.Highlights, h => h.Kind == HighlightKinds.NewNumberOne);
        Assert.Equal(7, highlight.PlayerId);
        Assert.Equal(971500, highlight.Score);
        Assert.Equal(970000, highlight.PrevValue);
    }

    [Fact]
    public void SameWeekRecordTiesCoCreditEveryClaimant()
    {
        var board = ChartBoard(level: 24, type: "Single", name: "Sudden Romance S24");
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[]
            {
                new PlacementRow(board.Id, 21, 1, 1_000_000),
                new PlacementRow(board.Id, 22, 1, 1_000_000),
                new PlacementRow(board.Id, 23, 3, 998000)
            },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 999000, 1) },
            folderRecords: new[] { new FolderRecordRow("Single", 24, 1_000_000, 1) }));

        var firsts = result.Highlights.Where(h => h.Kind == HighlightKinds.ChartGradeFirst).ToArray();
        Assert.Equal(2, firsts.Length);
        Assert.All(firsts, f => Assert.Equal("PG", f.GradeBand));
        Assert.Equal(new int?[] { 21, 22 }, firsts.Select(f => f.PlayerId).OrderBy(p => p).ToArray());
    }

    [Fact]
    public void DethronedCarriesThePreviousTopPlayerButNeverYourself()
    {
        var board = ChartBoard();
        var input = Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 972000) },
            previous: new[] { new PlacementRow(board.Id, 8, 1, 970000) },
            boardRecords: new[] { new BoardRecordRow(board.Id, 970000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 990000, 1) });

        var dethroning = HighlightsCalculator.Calculate(input);
        var selfImprovement = HighlightsCalculator.Calculate(input with
        {
            Previous = new[] { new PlacementRow(board.Id, 7, 1, 970000) }
        });

        Assert.Equal(8, Assert.Single(dethroning.Highlights,
            h => h.Kind == HighlightKinds.NewNumberOne).DethronedPlayerId);
        Assert.Null(Assert.Single(selfImprovement.Highlights,
            h => h.Kind == HighlightKinds.NewNumberOne).DethronedPlayerId);
    }

    [Fact]
    public void AGradeFirstAbsorbsItsNewNumberOne()
    {
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 991000) },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 988000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 995000, 1) }));

        Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.DoesNotContain(result.Highlights, h => h.Kind == HighlightKinds.NewNumberOne);
    }

    [Fact]
    public void AMultiBandJumpClaimsOnlyTheHighestNewBand()
    {
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 996100) },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 975000, 1) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 999000, 1) }));

        var first = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.Equal("SSS+", first.GradeBand);
    }

    [Fact]
    public void AFolderFirstAbsorbsTheChartFirstThatAchievedIt()
    {
        var achiever = ChartBoard();
        var sibling = ChartBoard(SecondChartBoardId, name: "District 1 D26");
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { achiever, sibling },
            current: new[]
            {
                new PlacementRow(achiever.Id, 7, 1, 1_000_000),
                new PlacementRow(sibling.Id, 8, 1, 991000)
            },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[]
            {
                new BoardRecordRow(achiever.Id, 998000, 1),
                new BoardRecordRow(sibling.Id, 989000, 1)
            },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 998000, 1) }));

        var folderFirst = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.FolderGradeFirst);
        Assert.Equal("PG", folderFirst.GradeBand);
        Assert.Equal(7, folderFirst.PlayerId);
        Assert.Equal(achiever.ChartId, folderFirst.ChartId);
        // The sibling board's own SSS first still stands; the achiever's chart first is absorbed.
        var chartFirst = Assert.Single(result.Highlights, h => h.Kind == HighlightKinds.ChartGradeFirst);
        Assert.Equal(8, chartFirst.PlayerId);
    }

    [Fact]
    public void HighlightsBelowLevel24NeverFire()
    {
        var board = ChartBoard(level: 23, name: "Mid Boss S23", type: "Single");
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 1_000_000) },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 980000, 1) },
            folderRecords: new[] { new FolderRecordRow("Single", 23, 980000, 1) }));

        Assert.DoesNotContain(result.Highlights,
            h => h.Kind is HighlightKinds.NewNumberOne or HighlightKinds.ChartGradeFirst
                or HighlightKinds.FolderGradeFirst);
        // The record book still advances — the gate is editorial, not archival.
        Assert.Equal(1_000_000, result.UpdatedBoardRecords.Single().HighScore);
    }

    [Fact]
    public void CoOpBoardsAreInvisibleToHighlightsAndRecords()
    {
        var board = new BoardDimension(ChartBoardId, LeaderboardTypes.Chart, "Coop Party CoOpx4",
            Guid.NewGuid(), "CoOp", 26);
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 1_000_000) },
            previous: Array.Empty<PlacementRow>()));

        // Only the pulse row remains, and the co-op entry never counted toward it.
        var pulse = Assert.Single(result.Highlights);
        Assert.Equal(HighlightKinds.WeeklyPulse, pulse.Kind);
        Assert.Equal(0, pulse.PrevValue);
        Assert.Equal(0, pulse.NewValue);
        Assert.Empty(result.UpdatedBoardRecords);
    }

    [Fact]
    public void BaselineEmitsNothingButPrimesBothRecordBooks()
    {
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 991000) },
            isBaseline: true));

        Assert.Empty(result.Highlights);
        var record = result.UpdatedBoardRecords.Single();
        Assert.Equal(991000, record.HighScore);
        Assert.Equal(SnapshotId, record.AchievedSnapshotId);
        var folder = result.UpdatedFolderRecords.Single();
        Assert.Equal("Double", folder.ChartType);
        Assert.Equal(26, folder.Level);
        Assert.Equal(991000, folder.HighScore);
    }

    [Fact]
    public void ABoardWithNoRecordYetPrimesSilentlyEvenOffBaseline()
    {
        // A brand-new board (or folder) gets the per-board baseline treatment: its first
        // sweep records the high without celebrating every score on it.
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 991000) },
            previous: Array.Empty<PlacementRow>()));

        // The entry still counts as activity (pulse, boards climbed), but nothing
        // celebrates it as a record moment.
        Assert.DoesNotContain(result.Highlights, h => h.Kind is HighlightKinds.NewNumberOne
            or HighlightKinds.ChartGradeFirst or HighlightKinds.FolderGradeFirst);
        Assert.Equal(991000, result.UpdatedBoardRecords.Single().HighScore);
    }

    [Fact]
    public void RecordsOnlyAdvanceAndStampTheAchievingSnapshot()
    {
        var board = ChartBoard();
        var result = HighlightsCalculator.Calculate(Input(
            boards: new[] { board },
            current: new[] { new PlacementRow(board.Id, 7, 1, 960000) },
            previous: Array.Empty<PlacementRow>(),
            boardRecords: new[] { new BoardRecordRow(board.Id, 970000, 3) },
            folderRecords: new[] { new FolderRecordRow("Double", 26, 970000, 3) }));

        Assert.Empty(result.UpdatedBoardRecords);
        Assert.Empty(result.UpdatedFolderRecords);
        Assert.DoesNotContain(result.Highlights, h => h.Kind is HighlightKinds.NewNumberOne
            or HighlightKinds.ChartGradeFirst or HighlightKinds.FolderGradeFirst);
    }
}
