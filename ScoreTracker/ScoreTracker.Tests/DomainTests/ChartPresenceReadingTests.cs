using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     What the presence graph draws from a chart's census rows (docs/design/chart-presence-graph.md §1,
///     §5): whose share it is, the title it is most held on, how it rates against its folder, and where
///     the viewer stands.
/// </summary>
public sealed class ChartPresenceReadingTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 16, 12, 30, 0, TimeSpan.Zero);
    private static readonly Guid ChartId = Guid.NewGuid();

    [Fact]
    public void NothingDrawsWithoutColumnsOrRows()
    {
        Assert.Null(ChartPresenceReading.Read(Array.Empty<ChartPresenceColumnRow>(), new[] { Row(0, 1) }, At, null,
            null));
        Assert.Null(ChartPresenceReading.Read(new[] { Column(0, "[P.B] BRONZE", 30) },
            Array.Empty<ChartPresenceRow>(), At, null, null));
    }

    [Fact]
    public void AChartWithoutAnOfficialRankingReadsTheColumnsCountedOverPiuScoresAccountsAlone()
    {
        // Everyone opens DIAMOND into its levels; the 80 PIU Scores accounts on it read DIAMOND whole.
        var columns = new[]
        {
            Column(0, "[P.B] DIAMOND LV.1", 200, 45), Column(1, "[P.B] DIAMOND LV.2", 200, 35),
            AccountsColumn(0, "[P.B] DIAMOND", 80)
        };

        var ranked = Read(columns, Row(0, 20), Row(1, 10));
        var unranked = Read(columns, Row(0, 20) with { CountsBoardPlayers = false });

        Assert.Equal(new[] { "[P.B] DIAMOND LV.1", "[P.B] DIAMOND LV.2" }, ranked.Columns.Select(c => c.Band.ToString()));
        Assert.Equal(0.1, ranked.Columns[0].Share);
        var diamond = Assert.Single(unranked.Columns);
        Assert.Equal(("[P.B] DIAMOND", (int?)null, 80, 0.25), (diamond.Band.ToString(), diamond.Level, diamond.Players,
            diamond.Share));
    }

    [Fact]
    public void AChartWhoseLayoutWasNeverWrittenDrawsNothing()
    {
        Assert.Null(ChartPresenceReading.Read(new[] { Column(0, "[P.B] DIAMOND", 400) },
            new[] { Row(0, 20) with { CountsBoardPlayers = false } }, At, null, null));
    }

    [Fact]
    public void TheMostHeldTitleIsTheHighestShareAmongTitlesWithEnoughPlayers()
    {
        var columns = new[]
        {
            Column(0, "[P.B] BRONZE", 10), Column(1, "[P.B] SILVER", 100), Column(2, "[P.B] GOLD", 50)
        };

        Assert.Equal(2, Read(columns, Row(0, 9), Row(1, 20), Row(2, 15)).MostHeld);
        // A title too thin to read wins only when nothing else holds the chart.
        Assert.Equal(0, Read(columns, Row(0, 9), Row(1, 0)).MostHeld);
        Assert.Null(Read(columns, Row(1, 0)).MostHeld);
    }

    [Fact]
    public void ItRatesHigherOrLowerOnlyFiveSpotsClearOfTheRestOfItsFolder()
    {
        var columns = new[] { Column(0, "[P.B] GOLD", 40), Column(1, "[P.B] PLATINUM", 40), Column(2, "[P.B] DIAMOND", 40) };

        var reading = Read(columns, Rated(0, median: 10, folderMedian: 15), Rated(1, median: 11, folderMedian: 15),
            Rated(2, median: 20, folderMedian: 15));

        Assert.Equal(new PumbilityPresenceRating?[]
        {
            PumbilityPresenceRating.Higher, PumbilityPresenceRating.Same, PumbilityPresenceRating.Lower
        }, reading.Columns.Select(c => c.Rating));
    }

    [Fact]
    public void ThereIsNoRatingWithoutEnoughPlayersHoldersOrFolderSpots()
    {
        var columns = new[]
        {
            Column(0, "[P.B] GOLD", 24), Column(1, "[P.B] PLATINUM", 40), Column(2, "[P.B] DIAMOND", 40),
            Column(3, "[P.B] RED BERYL", 40)
        };

        var reading = Read(columns, Rated(0, 10, 20), Rated(1, 10, 20) with { Holders = 4 },
            Rated(2, 10, 20) with { FolderSpots = 9 }, Rated(3, 10, 20));

        Assert.Equal(new PumbilityPresenceRating?[] { null, null, null, PumbilityPresenceRating.Higher },
            reading.Columns.Select(c => c.Rating));
    }

    [Fact]
    public void TheFolderShadowNeedsFiveSpotsToDraw()
    {
        var reading = Read(new[] { Column(0, "[P.B] GOLD", 40), Column(1, "[P.B] PLATINUM", 40) },
            Rated(0, 10, 20) with { FolderSpots = 4 }, Rated(1, 10, 20));

        Assert.Null(reading.Columns[0].Folder);
        Assert.NotNull(reading.Columns[1].Folder);
    }

    [Fact]
    public void EachGemTakesTheRatingMostOfItsHoldersSitUnderAndNeighboursThatAgreeShareARun()
    {
        var columns = new[]
        {
            Column(0, "[P.B] DIAMOND LV.1", 40), Column(1, "[P.B] DIAMOND LV.2", 40), Column(2, "[P.B] DIAMOND LV.3", 40),
            Column(3, "[P.B] RED BERYL LV.1", 40), Column(4, "[P.B] RED BERYL LV.2", 40), Column(5, "[P.B] ALEXANDRITE", 40)
        };

        var reading = Read(columns,
            Rated(0, 10, 20) with { Holders = 10 }, Rated(1, 15, 15) with { Holders = 6 },
            Rated(2, 10, 20) with { Holders = 5 },
            Rated(3, 10, 20) with { Holders = 8 }, Rated(4, 15, 15) with { Holders = 20 },
            Rated(5, 15, 15) with { Holders = 5 });

        Assert.Collection(reading.Ratings,
            run =>
            {
                Assert.Equal(PumbilityPresenceRating.Higher, run.Rating);
                Assert.Equal(new[] { Name.From("[P.B] DIAMOND") }, run.Gems);
            },
            run =>
            {
                Assert.Equal(PumbilityPresenceRating.Same, run.Rating);
                Assert.Equal(new[] { Name.From("[P.B] RED BERYL"), Name.From("[P.B] ALEXANDRITE") }, run.Gems);
            });
        Assert.Equal((Name.From("[P.B] DIAMOND"), (int?)2), (reading.Columns[1].Gem, reading.Columns[1].Level));
        Assert.Equal((Name.From("[P.B] ALEXANDRITE"), (int?)null), (reading.Columns[5].Gem, reading.Columns[5].Level));
    }

    [Fact]
    public void TheViewerStandsOnTheirOwnTitleWithTheChartsSpotInTheirTopFifty()
    {
        var columns = new[]
        {
            Column(0, "[P.B] DIAMOND LV.3", 40), Column(1, "[P.B] DIAMOND LV.4", 40), Column(2, "[P.B] DIAMOND LV.5", 40)
        };

        Assert.Equal(new PumbilityPresenceViewer(1, 11),
            ChartPresenceReading.Read(columns, new[] { Row(1, 5) }, At, 17_650, 11)!.Viewer);
        Assert.Equal(new PumbilityPresenceViewer(1, null),
            ChartPresenceReading.Read(columns, new[] { Row(1, 5) }, At, 17_650, null)!.Viewer);
        // Nowhere on these columns, and nobody signed in.
        Assert.Null(ChartPresenceReading.Read(columns, new[] { Row(1, 5) }, At, 9_000, null)!.Viewer);
        Assert.Null(ChartPresenceReading.Read(columns, new[] { Row(1, 5) }, At, null, null)!.Viewer);
    }

    private static ChartPumbilityPresenceRecord Read(IReadOnlyList<ChartPresenceColumnRow> columns,
        params ChartPresenceRow[] rows)
    {
        return ChartPresenceReading.Read(columns, rows, At, null, null)!;
    }

    private static ChartPresenceColumnRow Column(int order, string band, int players, int? sitePlayers = null)
    {
        return new ChartPresenceColumnRow(true, order, Name.From(band), players, sitePlayers ?? players);
    }

    private static ChartPresenceColumnRow AccountsColumn(int order, string band, int players)
    {
        return new ChartPresenceColumnRow(false, order, Name.From(band), players, players);
    }

    private static ChartPresenceRow Row(int column, int holders)
    {
        return new ChartPresenceRow(ChartId, column, true, holders,
            holders == 0 ? null : new PumbilitySpots(1, 10, 20, 30, 50), Array.Empty<double>(), 0, null);
    }

    /// <summary>Six holders around <paramref name="median" /> beside twelve folder spots around <paramref name="folderMedian" />.</summary>
    private static ChartPresenceRow Rated(int column, double median, double folderMedian)
    {
        return new ChartPresenceRow(ChartId, column, true, 6, new PumbilitySpots(1, median - 3, median, median + 3, 50),
            Array.Empty<double>(), 12, new PumbilitySpotBox(folderMedian - 3, folderMedian, folderMedian + 3));
    }
}
