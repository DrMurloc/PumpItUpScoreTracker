using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Folder Levels config panel and the grid it drives. The panel's own job is the popover
///     trigger and the cap; the greying-out is FolderGrid's, so it is pinned there directly —
///     MudPopover renders its content into a provider that a component-under-test has no tree for
///     (docs/design/folder-level-progression.md §6).
/// </summary>
public sealed class FolderLevelsConfigPanelTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();

    public FolderLevelsConfigPanelTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(_me, 5000, 26, 100, 0, 0, 868, 900000, 21.5,
                852, 900000, 21.3, 774, 880000, 19.9, 20.61, 21.34, 19.87));
        Services.AddSingleton(_mediator.Object);
        this.RenderInteractive();
    }

    private IRenderedComponent<FolderLevelsConfigPanel> Render(string sizePreset,
        params (ChartType Type, int Level)[] folders)
    {
        var config = new FolderLevelsConfig
        {
            Folders = folders.Select(f => new FolderLevelsTarget { Type = f.Type, Level = f.Level }).ToList()
        };
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "folder-levels", null, 0, sizePreset,
            WidgetConfigJson.Write(config), 1);
        return RenderComponent<FolderLevelsConfigPanel>(p => p.Add(c => c.Widget, widget));
    }

    [Fact]
    public void TheGridHidesBehindASelectFoldersButtonRatherThanSittingOnThePanel()
    {
        var cut = Render("2x2", (ChartType.Single, 22));

        var trigger = cut.Find(".fl-cfg-anchor button");
        Assert.Contains("Select Folders", trigger.TextContent);
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        trigger.Click();

        Assert.Equal("true", cut.Find(".fl-cfg-anchor button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void ThePanelSaysHowManySlotsTheSizeHolds()
    {
        Assert.Contains("1 of 7 folders", Render("2x3", (ChartType.Single, 22)).Markup);
        Assert.Contains("1 of 4 folders", Render("2x2", (ChartType.Single, 22)).Markup);
    }

    [Fact]
    public void PicksShowAsChipsSoTheChoiceIsVisibleWithTheGridClosed()
    {
        var cut = Render("2x2", (ChartType.Single, 22), (ChartType.Double, 18));

        Assert.Contains("S22", cut.Markup);
        Assert.Contains("D18", cut.Markup);
    }

    [Fact]
    public void AFullGridGreysTheCellsItCannotTakeAndKeepsThePickedOnesLive()
    {
        // FolderGrid's own contract: a host at its cap greys the rest rather than looking live
        // and swallowing the tap.
        var picked = new HashSet<int> { 20, 21, 22, 23 };
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(g => g.IsSelected, (t, l) => t == ChartType.Single && picked.Contains(l))
            .Add(g => g.IsDisabled, (t, l) => !(t == ChartType.Single && picked.Contains(l))));

        var cells = cut.FindAll(".folder-picker-level");
        var live = cells.Where(c => c.ClassList.Contains("folder-picker-current")).ToArray();
        var greyed = cells.Where(c => !c.ClassList.Contains("folder-picker-current")).ToArray();

        Assert.Equal(4, live.Length);
        Assert.All(live, c => Assert.False(c.HasAttribute("disabled")));
        Assert.All(greyed, c => Assert.True(c.HasAttribute("disabled")));
    }

    [Fact]
    public void RoomLeftMeansEveryCellStaysLive()
    {
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(g => g.IsSelected, (_, l) => l == 22)
            .Add(g => g.IsDisabled, (_, _) => false));

        Assert.All(cut.FindAll(".folder-picker-level"), c => Assert.False(c.HasAttribute("disabled")));
    }

    [Fact]
    public void ADisabledCellDoesNotRaiseAPick()
    {
        var picks = new List<int>();
        var cut = RenderComponent<FolderGrid>(p => p
            .Add(g => g.IsDisabled, (_, l) => l != 22)
            .Add(g => g.LevelPicked, f => picks.Add(f.Level)));

        cut.FindAll(".folder-picker-level").First(c => !c.HasAttribute("disabled")).Click();

        Assert.Equal(new[] { 22 }, picks);
    }
}
