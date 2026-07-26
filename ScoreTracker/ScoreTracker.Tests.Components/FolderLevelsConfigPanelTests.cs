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
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Folder Levels config panel: folders come from the shared grid behind a popover, and
///     the cell size caps how many fit (docs/design/folder-level-progression.md §6).
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

        Assert.Contains("Select Folders", cut.Markup);
        Assert.Empty(cut.FindAll(".folder-grid"));

        cut.Find(".fl-cfg-anchor button").Click();

        Assert.Single(cut.FindAll(".folder-grid"));
    }

    [Fact]
    public void AFullWidgetGreysTheCellsItCannotTakeAndKeepsThePickedOnesLive()
    {
        // 2x2 holds four; hand it four so the grid is at its cap on open.
        var cut = Render("2x2", (ChartType.Single, 20), (ChartType.Single, 21),
            (ChartType.Single, 22), (ChartType.Single, 23));
        cut.Find(".fl-cfg-anchor button").Click();

        var cells = cut.FindAll(".folder-picker-level");
        var picked = cells.Where(c => c.ClassList.Contains("folder-picker-current")).ToArray();
        var unpicked = cells.Where(c => !c.ClassList.Contains("folder-picker-current")).ToArray();

        Assert.Equal(4, picked.Length);
        Assert.All(picked, c => Assert.False(c.HasAttribute("disabled")));
        Assert.All(unpicked, c => Assert.True(c.HasAttribute("disabled")));
    }

    [Fact]
    public void RoomLeftMeansEveryCellStaysLive()
    {
        var cut = Render("2x2", (ChartType.Single, 22));
        cut.Find(".fl-cfg-anchor button").Click();

        Assert.All(cut.FindAll(".folder-picker-level"), c => Assert.False(c.HasAttribute("disabled")));
    }

    [Fact]
    public void ThePanelSaysHowManySlotsTheSizeHolds()
    {
        var cut = Render("2x3", (ChartType.Single, 22));

        Assert.Contains("1 of 7 folders", cut.Markup);
    }

    [Fact]
    public void DroppingAPickFreesTheGridAgain()
    {
        var cut = Render("2x1", (ChartType.Single, 22), (ChartType.Single, 23));
        cut.Find(".fl-cfg-anchor button").Click();
        Assert.Contains(cut.FindAll(".folder-picker-level"), c => c.HasAttribute("disabled"));

        // Tapping a picked cell removes it, which is how you make room.
        cut.FindAll(".folder-picker-level").First(c => c.ClassList.Contains("folder-picker-current")).Click();

        Assert.All(cut.FindAll(".folder-picker-level"), c => Assert.False(c.HasAttribute("disabled")));
    }
}
