using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     One session in full (docs/design/march-of-murlocs.md §11.3): the four numbers with their
///     places on the board, a draft that is its owner's alone, and the owner's past seasons on
///     one lineage.
/// </summary>
public sealed class MoMReadHandlerSessionTests
{
    [Fact]
    public async Task ASessionCarriesItsPlaceItsFourNumbersAndWhereEachStandsOnTheBoard()
    {
        var w = MoMReadHandlerWorld.Build();

        var view = await w.F.Handler().Handle(new GetMoMSessionQuery(w.Kim.Id), CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal(1, view!.Place);
        Assert.Equal(3, view.Of);
        Assert.Equal(3000, view.TotalScore);
        Assert.Equal("Winter 2025", view.Season.Name);
        Assert.Equal(ChartType.Double, view.ChartType);
        Assert.Equal("김재현", view.Player!.Name.ToString());
        Assert.False(view.IsDraft);
        Assert.Equal(2, view.Levers.ChartsPlayed);
        Assert.Equal(3000, view.Levers.TotalScore);
        Assert.Equal(24.0, view.Levers.AverageBalancedLevel, 2); // 24.5 and 23.5, no overrides
        // Charts: the third session played three, so this one is second of three.
        Assert.Equal(2, view.Places.Charts);
        Assert.Equal(1, view.Places.Grade); // the cleanest average score on the board
        Assert.Equal(3, view.Places.Of);
        Assert.Equal(2, view.Charts.Count);
        Assert.Equal("Slam", view.Charts[0].Chart.Chart.Song.Name.ToString());
        Assert.Equal(TimeSpan.Zero, view.Charts[0].StartsAt);
        Assert.Equal(3, view.BoardSessions.Count);
        Assert.Equal(new[] { 1, 2, 3 }, view.BoardSessions.Select(s => s.Place));
        Assert.Equal(w.Tieny.Id, view.BoardSessions[1].SessionId);
    }

    [Fact]
    public async Task TheOwnersPastSessionsWalkTheBoardLineageOnly()
    {
        var w = MoMReadHandlerWorld.Build();

        var view = await w.F.Handler().Handle(new GetMoMSessionQuery(w.Kim.Id), CancellationToken.None);

        var past = Assert.Single(view!.OwnersPastSessions);
        Assert.Equal(w.KimBefore.Id, past.SessionId);
        Assert.Equal("March of Murlocs 2", past.Season.Name);
        Assert.Equal(2400, past.TotalScore);
    }

    [Fact]
    public async Task ADraftIsItsOwnersAlone()
    {
        var w = MoMReadHandlerWorld.Build();
        var owner = w.F.User("drafter");
        var draft = w.F.Session(w.Doubles, owner, 5000, null);
        w.F.Row(draft, w.F.Chart("Slam", 24, 99), 980000, 1700, 0);

        Assert.Null(await w.F.Handler().Handle(new GetMoMSessionQuery(draft.Id), CancellationToken.None));
        Assert.Null(await w.F.Handler().Handle(new GetMoMSessionQuery(draft.Id, w.KimId), CancellationToken.None));
        var mine = await w.F.Handler().Handle(new GetMoMSessionQuery(draft.Id, owner.Id), CancellationToken.None);
        Assert.NotNull(mine);
        Assert.True(mine!.IsDraft);
        Assert.Equal(0, mine.Place); // not on the board
        Assert.Equal(3, mine.Of);
        Assert.Equal(1, mine.Levers.ChartsPlayed);
        Assert.Equal(3, mine.BoardSessions.Count); // the draft is not among them
    }

    [Fact]
    public async Task AnUnknownSessionIsNull()
    {
        var w = MoMReadHandlerWorld.Build();
        Assert.Null(await w.F.Handler().Handle(new GetMoMSessionQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task AChartTheCatalogNoLongerKnowsDropsFromTheListNotFromTheTotal()
    {
        var w = MoMReadHandlerWorld.Build();
        w.F.Charts.RemoveAll(c => c.Id == w.Odin);

        var view = await w.F.Handler().Handle(new GetMoMSessionQuery(w.Kim.Id), CancellationToken.None);

        Assert.Single(view!.Charts);
        Assert.Equal(3000, view.TotalScore);
        Assert.Equal(1, view.Levers.ChartsPlayed);
    }
}
