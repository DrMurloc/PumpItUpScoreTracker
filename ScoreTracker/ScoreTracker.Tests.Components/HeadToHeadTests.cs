using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Components.Players;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The head-to-head component's one new behaviour: the charts only one of you has played are
///     in the record but off the table until the switch says otherwise, and the switch itself only
///     exists when there is something to switch on.
/// </summary>
public sealed class HeadToHeadTests : ComponentTestBase
{
    private static readonly Chart Shared = ChartSlugsTests.BuildChart(song: "Redline", level: 22);
    private static readonly Chart Mine = ChartSlugsTests.BuildChart(song: "Meteo5cience (GADGET mix)", level: 22);
    private static readonly Chart Theirs = ChartSlugsTests.BuildChart(song: "Baroque Virus", level: 22);

    private static readonly HeadToHeadSubject Reno = new(Guid.NewGuid(), "RENO", null);

    private static RivalHeadToHeadRecord WithUnshared() => new(Reno, 0, 1, 1, new[]
    {
        new RivalHeadToHeadRow(Shared.Id, 981_204, 995_880, RivalScoreSource.Site),
        new RivalHeadToHeadRow(Theirs.Id, null, 966_120, RivalScoreSource.Site),
        new RivalHeadToHeadRow(Mine.Id, 984_730, null, RivalScoreSource.Site)
    }, OnlyYou: 1, OnlyThem: 1);

    private static RivalHeadToHeadRecord SharedOnly() => new(Reno, 0, 1, 1, new[]
    {
        new RivalHeadToHeadRow(Shared.Id, 981_204, 995_880, RivalScoreSource.Site)
    });

    private IRenderedComponent<HeadToHead> Render(RivalHeadToHeadRecord record)
    {
        this.RenderInteractive();
        var charts = new Dictionary<Guid, Chart> { [Shared.Id] = Shared, [Mine.Id] = Mine, [Theirs.Id] = Theirs };
        return RenderComponent<HeadToHead>(p => p
            .Add(c => c.Record, record)
            .Add(c => c.Charts, charts)
            .Add(c => c.Mix, MixEnum.Phoenix2));
    }

    [Fact]
    public void OneSidedRowsAreOffTheTableUntilAsked()
    {
        var cut = Render(WithUnshared());

        Assert.Single(cut.FindAll("[data-testid='rival-compare-table'] tbody tr"));
        Assert.Contains("1 charts", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid='only-you-tile']"));
        // The switch names how many rows it would add.
        Assert.Equal("Show 2 unshared charts", cut.FindComponents<MudSwitch<bool>>()[0].Instance.Label);
    }

    [Fact]
    public async Task TheSwitchRevealsTheOneSidedRowsAndTheirTiles()
    {
        var cut = Render(WithUnshared());

        await cut.FindComponents<MudSwitch<bool>>()[0].Find("input")
            .ChangeAsync(new ChangeEventArgs { Value = true });

        Assert.Equal(3, cut.FindAll("[data-testid='rival-compare-table'] tbody tr").Count);
        Assert.Equal(2, cut.FindAll("tr.rvl-cmp-unshared").Count);
        Assert.Contains("Only you", cut.Find("[data-testid='only-you-tile']").TextContent);
        Assert.Contains("Only RENO", cut.Find("[data-testid='only-them-tile']").TextContent);
        Assert.Contains("3 charts", cut.Markup);
    }

    [Fact]
    public void NoSwitchWhenEveryRowIsShared()
    {
        var cut = Render(SharedOnly());

        Assert.Empty(cut.FindComponents<MudSwitch<bool>>());
        Assert.Single(cut.FindAll("[data-testid='rival-compare-table'] tbody tr"));
    }
}
