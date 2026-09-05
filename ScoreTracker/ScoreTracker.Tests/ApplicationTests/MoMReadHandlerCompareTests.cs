using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.EventCompetition.Contracts.Queries;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Two sessions side by side (docs/design/march-of-murlocs.md §11.3): on one board the shared
///     charts lead with the worst gap and nothing is re-priced; across seasons the older session
///     is re-priced under the newer one (D20); across chart types nothing is offered (D15).
/// </summary>
public sealed class MoMReadHandlerCompareTests
{
    [Fact]
    public async Task ComparingOnOneBoardListsTheSharedChartsWorstGapFirstAndRepricesNothing()
    {
        var w = MoMReadHandlerWorld.Build();

        var cmp = await w.F.Handler().Handle(new CompareMoMSessionsQuery(w.Kim.Id, w.Tieny.Id), CancellationToken.None);

        Assert.NotNull(cmp);
        Assert.True(cmp!.SameBoard);
        Assert.Null(cmp.Repricing);
        Assert.Equal("tieny", cmp.Other!.Name.ToString());
        Assert.Equal(2, cmp.Mine.ChartsPlayed);
        Assert.Equal(2, cmp.Theirs.ChartsPlayed);
        var shared = Assert.Single(cmp.Shared);
        Assert.Equal(w.Slam, shared.Chart.Id);
        Assert.Equal(1600 - 1650, shared.Gap);
    }

    [Fact]
    public async Task ComparingAcrossSeasonsRepricesTheOlderSessionUnderTheNewerOne()
    {
        var w = MoMReadHandlerWorld.Build();

        var cmp = await w.F.Handler().Handle(new CompareMoMSessionsQuery(w.Kim.Id, w.KimBefore.Id), CancellationToken.None);

        Assert.NotNull(cmp);
        Assert.False(cmp!.SameBoard);
        Assert.False(cmp.OlderIsMine);
        Assert.Equal("March of Murlocs 2", cmp.OtherSeason.Name);
        Assert.NotNull(cmp.Repricing);
        Assert.Equal(2400, cmp.Repricing!.OldTotal);
        // Winter 2025 pays level 24 three hundred more, so the old session is worth more today;
        // neither season overrides a chart's balance, so the balance moved nothing.
        Assert.True(cmp.Repricing.TablesReCut > 0);
        Assert.Equal(0, cmp.Repricing.ChartsReRated);
        Assert.Equal(2, cmp.Shared.Count); // biggest gain first across seasons
        Assert.True(cmp.Shared[0].Gap >= cmp.Shared[1].Gap);

        var reversed = await w.F.Handler().Handle(new CompareMoMSessionsQuery(w.KimBefore.Id, w.Kim.Id), CancellationToken.None);
        Assert.True(reversed!.OlderIsMine);
        Assert.Equal(cmp.Repricing.RepricedTotal, reversed.Repricing!.RepricedTotal);
    }

    [Fact]
    public async Task ComparingAcrossChartTypesIsNeverOffered()
    {
        var w = MoMReadHandlerWorld.Build();
        var singles = w.F.Sessions.Single(s => s.BoardId != w.Doubles.Id && s.BoardId != w.OldDoubles.Id);

        Assert.Null(await w.F.Handler().Handle(new CompareMoMSessionsQuery(w.Kim.Id, singles.Id), CancellationToken.None));
        Assert.Null(await w.F.Handler().Handle(new CompareMoMSessionsQuery(w.Kim.Id, Guid.NewGuid()), CancellationToken.None));
    }
}
