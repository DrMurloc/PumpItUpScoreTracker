using System;
using System.Linq;
using ScoreTracker.OfficialMirror.Domain;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The merge rules behind the supplemented leaderboards: one row per human, the higher
///     score wins, ties are ordered rather than arbitrary, and on a complete chart board our
///     players can only append below the official tail.
/// </summary>
public sealed class SupplementMergeTests
{
    private const int Board = 7;

    private static PlacementRow Official(int playerId, int place, decimal score) =>
        new(Board, playerId, place, score);

    [Fact]
    public void APlayerAbsentFromTheOfficialBoardIsStoredBelowIt()
    {
        var official = new[] { Official(1, 1, 990_000), Official(2, 2, 985_000), Official(3, 3, 980_000) };

        var stored = SupplementMerge.RowsToStore(Board, official, new[] { (PlayerId: 9, Score: 970_000m) });

        var row = Assert.Single(stored);
        Assert.Equal(9, row.PlayerId);
        Assert.Equal(4, row.Place);
        Assert.True(row.IsSupplemented);
    }

    [Fact]
    public void APlayerAlreadyOnTheBoardWithNoBetterScoreIsNotStoredAtAll()
    {
        var official = new[] { Official(1, 1, 990_000), Official(2, 2, 985_000) };

        // Equal, not merely lower: re-storing an identical score would put the same human on
        // the board twice for no gain.
        var stored = SupplementMerge.RowsToStore(Board, official,
            new[] { (PlayerId: 1, Score: 990_000m), (PlayerId: 2, Score: 900_000m) });

        Assert.Empty(stored);
    }

    [Fact]
    public void APlayerOnTheBoardWhoseLedgerScoreIsHigherIsStoredAndTakesTheHigherPlace()
    {
        // The board is a week old and they improved since the scrape.
        var official = new[] { Official(1, 1, 990_000), Official(2, 2, 985_000), Official(3, 3, 980_000) };

        var stored = SupplementMerge.RowsToStore(Board, official, new[] { (PlayerId: 3, Score: 995_000m) });

        var row = Assert.Single(stored);
        Assert.Equal(3, row.PlayerId);
        Assert.Equal(1, row.Place);
    }

    [Fact]
    public void TheMergedBoardShowsOneRowPerHumanEvenWhenBothReadingsHoldThem()
    {
        var official = new[] { Official(1, 1, 990_000), Official(3, 2, 980_000) };
        var supplemented = new PlacementRow(Board, 3, 1, 995_000, true);

        var merged = SupplementMerge.MergedBoard(official.Append(supplemented));

        Assert.Equal(2, merged.Count);
        Assert.Equal(new[] { 3, 1 }, merged.Select(r => r.PlayerId));
        Assert.Equal(995_000, merged[0].Score);
    }

    [Fact]
    public void TiesTakeTheSamePlaceAndTheNextPlaceSkips()
    {
        var official = new[] { Official(1, 1, 990_000), Official(2, 1, 990_000) };

        var merged = SupplementMerge.MergedBoard(official.Append(new PlacementRow(Board, 9, 0, 980_000, true)));

        Assert.Equal(new[] { 1, 1, 3 }, merged.Select(r => r.Place));
    }

    [Fact]
    public void AtAnEqualScoreTheOfficialRowIsListedFirst()
    {
        var official = new[] { Official(4, 1, 990_000) };

        var merged = SupplementMerge.MergedBoard(official.Append(new PlacementRow(Board, 1, 0, 990_000, true)));

        // Same place — but a fixed order within it, so pagination is stable across renders.
        Assert.Equal(new[] { 1, 1 }, merged.Select(r => r.Place));
        Assert.Equal(4, merged[0].PlayerId);
        Assert.False(merged[0].IsSupplemented);
    }

    [Fact]
    public void OnACompleteBoardNothingSupplementedOutplacesTheOfficialTail()
    {
        var official = Enumerable.Range(1, 300)
            .Select(i => Official(i, i, 1_000_000 - i * 100))
            .ToArray();
        var floor = official[^1].Score;

        // Every ledger candidate is below the 300th score, which is what "not on the board"
        // means on a board the sweep read in full.
        var stored = SupplementMerge.RowsToStore(Board, official,
            new[] { (PlayerId: 901, Score: floor - 1), (PlayerId: 902, Score: floor - 5_000) });

        Assert.Equal(new[] { 301, 302 }, stored.Select(r => r.Place));
        Assert.Equal(0, SupplementMerge.RowsAboveOfficialTail(
            SupplementMerge.MergedBoard(official.Concat(stored))));
    }

    [Fact]
    public void ASkippedOfficialBoardLeavesOurPlayersAloneOnItAndSaysSo()
    {
        var stored = SupplementMerge.RowsToStore(Board, Array.Empty<PlacementRow>(),
            new[] { (PlayerId: 901, Score: 970_000m), (PlayerId: 902, Score: 960_000m) });

        Assert.Equal(new[] { 1, 2 }, stored.Select(r => r.Place));
        Assert.Equal(2, SupplementMerge.RowsAboveOfficialTail(SupplementMerge.MergedBoard(stored)));
    }

    [Fact]
    public void ARatingBoardInterleavesBecauseTheTwoSidesAreDifferentMeasurements()
    {
        // The PUMBILITY board is the one place a supplemented row can land mid-board: the
        // official value is piugame's and ours is computed, so ours is not bounded by theirs.
        var official = new[] { Official(1, 1, 1_040m), Official(2, 2, 1_020m), Official(3, 3, 990m) };

        var stored = SupplementMerge.RowsToStore(Board, official, new[] { (PlayerId: 9, Score: 1_014m) });

        Assert.Equal(3, Assert.Single(stored).Place);
    }
}
