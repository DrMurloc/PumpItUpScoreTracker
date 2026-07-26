using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.HomePage.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.HomeWidgets;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Folder Levels widget at each of its sizes. The point of the size ladder is that
///     detail drops rather than the layout changing, so these pin what each cell shows
///     (docs/design/folder-level-progression.md §6).
/// </summary>
public sealed class FolderLevelsWidgetTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _me = Guid.NewGuid();
    private readonly List<Chart> _charts = new();
    private readonly List<RecordedPhoenixScore> _scores = new();

    public FolderLevelsWidgetTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(_me, "Me", true, null, new Uri("https://piu.test/me.png"), null));

        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _charts.ToArray());
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _scores.ToArray());
        Services.AddSingleton(_mediator.Object);
        // Rows nest DifficultyBubble, which gates its MudTooltip on RendererInfo; declare the
        // render world so bUnit can supply it.
        this.RenderInteractive();
    }

    /// <summary>A folder of <paramref name="size" /> charts, the first <paramref name="scores" /> passed.</summary>
    private void GivenFolder(ChartType type, int level, int size, params int[] scores)
    {
        for (var i = 0; i < size; i++)
        {
            var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix,
                new Song(Name.From($"Song {type}{level}-{i}"), SongType.Arcade,
                    new Uri("https://piu.test/s.png"), TimeSpan.FromMinutes(2), Name.From("Artist"), null),
                type, DifficultyLevel.From(level), MixEnum.Phoenix, null, null, new HashSet<Skill>());
            _charts.Add(chart);
            if (i < scores.Length)
                _scores.Add(new RecordedPhoenixScore(chart.Id, PhoenixScore.From(scores[i]),
                    PhoenixPlate.FairGame, false, DateTimeOffset.UnixEpoch));
        }
    }

    private IRenderedComponent<FolderLevelsWidget> Render(string sizePreset,
        params (ChartType Type, int Level)[] folders)
    {
        var config = new FolderLevelsConfig
        {
            Folders = folders.Select(f => new FolderLevelsTarget { Type = f.Type, Level = f.Level }).ToList()
        };
        var widget = new HomePageWidgetRecord(Guid.NewGuid(), "folder-levels", null, 0, sizePreset,
            WidgetConfigJson.Write(config), 1);
        return RenderComponent<FolderLevelsWidget>(p => p
            .Add(w => w.Widget, widget)
            .Add(w => w.EffectiveMix, MixEnum.Phoenix));
    }

    [Fact]
    public void OneByOneGivesASingleFolderTheWholeCell()
    {
        GivenFolder(ChartType.Single, 22, 10, 930000, 930000, 930000, 930000, 930000);

        var cut = Render("1x1", (ChartType.Single, 22));

        Assert.Single(cut.FindAll(".dash-fl-hero"));
        Assert.Contains("AA+", cut.Markup);
        Assert.Contains("50% complete", cut.Markup);
        Assert.Contains("5 to Folder Lamp", cut.Markup);
    }

    [Fact]
    public void TwoByOneStacksARowPerFolderWithoutTicks()
    {
        GivenFolder(ChartType.Single, 22, 10, 930000, 930000, 930000, 930000, 930000, 930000);
        GivenFolder(ChartType.Double, 18, 4, 990000, 990000);

        var cut = Render("2x1", (ChartType.Single, 22), (ChartType.Double, 18));

        Assert.Equal(2, cut.FindAll(".dash-fl-row").Count);
        Assert.Empty(cut.FindAll(".fl-tick"));
    }

    [Fact]
    public void TwoRowsAreEnoughToCutTheTierTicksIn()
    {
        GivenFolder(ChartType.Single, 22, 10, 930000);

        var cut = Render("2x2", (ChartType.Single, 22));

        // 20/40/60/80 — the lamp is the flag at the end, not a tick.
        Assert.Equal(4, cut.FindAll(".fl-tick").Count);
    }

    [Fact]
    public void OnlyTheWidestSizeSpendsRoomOnChartCounts()
    {
        GivenFolder(ChartType.Single, 22, 10, 930000, 930000);

        var narrow = Render("2x2", (ChartType.Single, 22));
        var wide = Render("4x2", (ChartType.Single, 22));

        Assert.Empty(narrow.FindAll(".dash-fl-row-count"));
        Assert.Contains("2 of 10", wide.Markup);
    }

    [Fact]
    public void ALampedFolderSaysSoInsteadOfShowingAHundredPercent()
    {
        GivenFolder(ChartType.Single, 24, 3, 880000, 890000, 900000);

        var cut = Render("2x1", (ChartType.Single, 24));

        Assert.Contains("Folder Lamp", cut.Markup);
        Assert.DoesNotContain("100% complete", cut.Markup);
    }

    [Fact]
    public void AFolderWithNoChartsInTheMixIsDroppedRatherThanRenderedEmpty()
    {
        GivenFolder(ChartType.Single, 22, 4, 930000);

        var cut = Render("2x1", (ChartType.Single, 22), (ChartType.Single, 26));

        Assert.Single(cut.FindAll(".dash-fl-row"));
    }

    [Fact]
    public void ConfiguringNoFoldersAsksForSome()
    {
        var cut = Render("2x1");

        Assert.Contains("Pick the folders you're working on.", cut.Markup);
    }

    [Fact]
    public void AnUntouchedFolderShowsNoGradeRatherThanAnF()
    {
        GivenFolder(ChartType.Single, 25, 8);

        var cut = Render("2x1", (ChartType.Single, 25));

        Assert.Contains("0% complete", cut.Markup);
        Assert.Contains("var(--unplayed-grade)", cut.Markup);
    }
}
