using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
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
        bool isBaseline = false)
    {
        return new HighlightsInput(SnapshotId, isBaseline,
            (boards ?? new[] { Pumbility }).ToArray(),
            (current ?? Array.Empty<PlacementRow>()).ToArray(),
            previous?.ToArray(),
            (boardRecords ?? Array.Empty<BoardRecordRow>()).ToArray(),
            (folderRecords ?? Array.Empty<FolderRecordRow>()).ToArray());
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
        Assert.Equal(new[] { 1, 2, 3 }, movers.Select(m => m.PlayerId).ToArray());
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
    public void BoardsClimbedCountsImprovementsAndNewEntriesAboveTheMinimum()
    {
        var boards = Enumerable.Range(100, 6).Select(id => ChartBoard(id, name: $"Board {id}")).ToArray();
        // Player 1 climbs 4 boards and enters a 5th fresh; player 2 climbs only 1.
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

        var climbed = result.Highlights.Where(h => h.Kind == HighlightKinds.BoardsClimbed).ToArray();
        var playerOne = Assert.Single(climbed);
        Assert.Equal(1, playerOne.PlayerId);
        Assert.Equal(5, playerOne.NewValue); // four climbs + one new entry
        Assert.Equal(40, playerOne.PrevValue); // net places from the four climbed boards only
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
        Assert.Equal(new[] { 21, 22 }, firsts.Select(f => f.PlayerId).OrderBy(p => p).ToArray());
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

        Assert.Empty(result.Highlights);
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

        Assert.Empty(result.Highlights);
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
        Assert.Empty(result.Highlights);
    }
}
