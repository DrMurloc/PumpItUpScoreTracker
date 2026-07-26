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
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Contracts.Queries;
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
        // Singles 21.34 / doubles 19.87 — what a fresh widget centres its folder picks on.
        _mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(_me, 5000, 26, 100, 0, 0, 868, 900000, 21.5,
                852, 900000, 21.3, 774, 880000, 19.9, 20.61, 21.34, 19.87));
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
        // Nothing else in a 1x1 names the folder, so the ball has to — and the ball is art, so
        // the folder's name rides its alt text.
        Assert.Equal("S22", cut.Find(".dash-fl-hero img").GetAttribute("alt"));
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

        // 20/40/60/80 — 100 is the track own end, so it never gets one.
        Assert.Equal(4, cut.FindAll(".fl-tick").Count);
    }

    [Fact]
    public void TheCompactSizeDropsChartCountsAndEverythingTallerKeepsThem()
    {
        // A row is the same height at every stacked size, so detail splits on one line rather
        // than easing in: 2x1 is the compact variant, 2x2 and taller carry the counts.
        GivenFolder(ChartType.Single, 22, 10, 930000, 930000);

        var compact = Render("2x1", (ChartType.Single, 22));
        var tall = Render("2x2", (ChartType.Single, 22));

        Assert.Empty(compact.FindAll(".dash-fl-row-count"));
        Assert.Contains("2 of 10", tall.Markup);
    }

    [Theory]
    [InlineData("1x1", 1)]
    [InlineData("2x1", 2)]
    [InlineData("2x2", 4)]
    [InlineData("2x3", 7)]
    public void EachSizeShowsExactlyAsManyFoldersAsItHoldsRoomFor(string sizePreset, int capacity)
    {
        // Ten configured folders, so every size is asked for more than it can hold.
        var targets = Enumerable.Range(16, 10)
            .Select(level => (ChartType.Single, level)).ToArray();
        foreach (var (_, level) in targets) GivenFolder(ChartType.Single, level, 4, 930000);

        var cut = Render(sizePreset, targets);

        var shown = sizePreset == "1x1"
            ? cut.FindAll(".dash-fl-hero").Count
            : cut.FindAll(".dash-fl-row").Count;
        Assert.Equal(capacity, shown);
    }

    [Fact]
    public void AFreshWidgetFillsItselfAroundYourCompetitiveLevel()
    {
        // Nothing configured: the widget picks folders rather than showing an empty prompt.
        for (var level = 15; level <= 25; level++)
        {
            GivenFolder(ChartType.Single, level, 4, 930000);
            GivenFolder(ChartType.Double, level, 4, 930000);
        }

        var cut = Render("2x2");

        Assert.Equal(4, cut.FindAll(".dash-fl-row").Count);
        // Singles 21.3 and doubles 19.9 (see the stats stub): own level first, then upward.
        Assert.Contains("S21", cut.Markup);
        Assert.Contains("D19", cut.Markup);
        Assert.Contains("S22", cut.Markup);
        Assert.Contains("D20", cut.Markup);
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
    public void AMixWithNoChartsAtAllStillAsksForFolders()
    {
        // Nothing to suggest from, so the empty prompt is the honest state.
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
