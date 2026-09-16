using System;
using System.Collections.Generic;
using System.Linq;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     The PUMBILITY presence census (docs/design/chart-presence-graph.md §2–§4): which columns a ladder
///     has, what a spot is, and what a chart's row on a column carries.
/// </summary>
public sealed class ChartPresenceCensusTests
{
    private static int _boardPlayers;

    [Fact]
    public void AGemOpensIntoItsLevelsOnceItHoldsTwentyFivePlayersALevel()
    {
        var diamond = Enumerable.Repeat(17_500.0, 124).ToArray();

        Assert.Equal(8, ChartPresenceCensus.Columns(diamond).Count);

        var opened = ChartPresenceCensus.Columns(diamond.Append(17_900)).ToArray();
        Assert.Equal(12, opened.Length);
        Assert.Equal(new int?[] { 1, 2, 3, 4, 5 },
            opened.Where(band => band.Gem == Name.From("[P.B] DIAMOND")).Select(band => band.Level));
        Assert.Equal(Name.From("ABYSS ABSOLUTE"), opened[^1].Name);
    }

    [Fact]
    public void ChartsWorthTheSameShareTheMiddleOfTheSpotsTheyFill()
    {
        var charts = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

        var spots = ChartPresenceCensus.Spots(new[]
        {
            new PoolSlot(charts[0], 400), new PoolSlot(charts[1], 380), new PoolSlot(charts[2], 380),
            new PoolSlot(charts[3], 380), new PoolSlot(charts[4], 350)
        });
        var pair = ChartPresenceCensus.Spots(new[] { new PoolSlot(charts[0], 400), new PoolSlot(charts[1], 400) });

        Assert.Equal(new[] { 1.0, 3, 3, 3, 5 }, charts.Select(c => spots[c]));
        Assert.Equal(1.5, pair[charts[0]]);
        Assert.Equal(1.5, pair[charts[1]]);
    }

    [Fact]
    public void ATitleCountsEveryoneOnItAndThePiuScoresAccountsAmongThem()
    {
        var chart = Chart(22);

        var census = ChartPresenceCensus.Take(new[]
        {
            Account(10_200, chart.Id), Board(10_300), Account(12_600),
            // Under BRONZE: on no title, so neither counted nor holding anything the census sees.
            Account(9_000, chart.Id)
        }, Catalog(chart), Ranked(chart));

        Assert.Equal(Name.From("[P.B] BRONZE"), census.Columns[0].Band);
        Assert.Equal((2, 1), (census.Columns[0].Players, census.Columns[0].SitePlayers));
        Assert.Equal((1, 1), (census.Columns[1].Players, census.Columns[1].SitePlayers));
        var row = Assert.Single(census.Rows);
        Assert.Equal((0, 1), (row.Column, row.Holders));
    }

    [Fact]
    public void AChartsRowCarriesItsHoldersSpotsAndTheRestOfItsFolderWithoutItself()
    {
        var chart = Chart(22);
        var folderMate = Chart(22);
        var elsewhere = Chart(23);
        var voices = Enumerable.Range(0, 6).Select(_ => Account(10_100, folderMate.Id, chart.Id, elsewhere.Id))
            .ToArray();

        var census = ChartPresenceCensus.Take(voices, Catalog(chart, folderMate, elsewhere),
            Ranked(chart, folderMate, elsewhere));

        var row = census.Rows.Single(r => r.ChartId == chart.Id);
        Assert.Equal(6, row.Holders);
        Assert.Equal(new PumbilitySpots(2, 2, 2, 2, 2), row.Spots);
        Assert.Empty(row.Dots);
        Assert.Equal(6, row.FolderSpots);
        Assert.Equal(new PumbilitySpotBox(1, 1, 1), row.Folder);
        // A chart alone in its folder has no rest of the folder to sit beside.
        var alone = census.Rows.Single(r => r.ChartId == elsewhere.Id);
        Assert.Equal((0, (PumbilitySpotBox?)null), (alone.FolderSpots, alone.Folder));
    }

    [Fact]
    public void UnderFiveHoldersTheSpotsAreKeptOneByOne()
    {
        var chart = Chart(22);
        var ahead = new[] { Chart(24), Chart(24), Chart(24) };
        var voices = Enumerable.Range(0, 4)
            .Select(i => Account(10_100, ahead.Take(i).Select(c => c.Id).Append(chart.Id).ToArray()))
            .ToArray();

        var census = ChartPresenceCensus.Take(voices, Catalog(ahead.Append(chart).ToArray()),
            Ranked(ahead.Append(chart).ToArray()));

        var row = census.Rows.Single(r => r.ChartId == chart.Id);
        Assert.Equal(new[] { 1.0, 2, 3, 4 }, row.Dots);
        Assert.Equal(new PumbilitySpots(1, 1.75, 2.5, 3.25, 4), row.Spots);
    }

    [Fact]
    public void AChartNobodyHoldsHasARowOnlyWhereTheRestOfItsFolderIsHeld()
    {
        var held = Chart(22);
        var unheld = Chart(22);
        var lonely = Chart(24);

        var census = ChartPresenceCensus.Take(new[] { Account(10_100, held.Id) }, Catalog(held, unheld, lonely),
            Ranked(held, unheld, lonely));

        var row = census.Rows.Single(r => r.ChartId == unheld.Id);
        Assert.Equal((0, (PumbilitySpots?)null, 1), (row.Holders, row.Spots, row.FolderSpots));
        Assert.DoesNotContain(census.Rows, r => r.ChartId == lonely.Id);
    }

    [Fact]
    public void AChartWithoutAnOfficialRankingIsMarkedSo()
    {
        var ranked = Chart(22);
        var unranked = Chart(19);

        var census = ChartPresenceCensus.Take(new[] { Account(10_100, ranked.Id, unranked.Id) },
            Catalog(ranked, unranked), Ranked(ranked));

        Assert.True(census.Rows.Single(r => r.ChartId == ranked.Id).CountsBoardPlayers);
        Assert.False(census.Rows.Single(r => r.ChartId == unranked.Id).CountsBoardPlayers);
    }

    private static PresenceVoice Account(double pumbility, params Guid[] fifty)
    {
        return new PresenceVoice(PeerVoice.Account(Guid.NewGuid()), pumbility, false, Fifty(fifty));
    }

    private static PresenceVoice Board(double pumbility, params Guid[] fifty)
    {
        return new PresenceVoice(PeerVoice.FromBoard(++_boardPlayers, $"BOARD#{_boardPlayers}"), pumbility, true,
            Fifty(fifty));
    }

    /// <summary>A fifty in the order given, each chart worth less than the one before it.</summary>
    private static IReadOnlyList<PoolSlot> Fifty(IEnumerable<Guid> charts)
    {
        return charts.Select((chart, i) => new PoolSlot(chart, 400 - i)).ToArray();
    }

    private static Chart Chart(int level)
    {
        return new ChartBuilder().WithId(Guid.NewGuid()).WithMix(MixEnum.Phoenix2).WithType(ChartType.Single)
            .WithLevel(level).Build();
    }

    private static IReadOnlyDictionary<Guid, Chart> Catalog(params Chart[] charts)
    {
        return charts.ToDictionary(c => c.Id);
    }

    private static IReadOnlySet<Guid> Ranked(params Chart[] charts)
    {
        return charts.Select(c => c.Id).ToHashSet();
    }
}
