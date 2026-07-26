using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Pages.Tools;
using ScoreTracker.Web.Services.Contracts;
using Xunit;
using Chart = ScoreTracker.SharedKernel.Models.Chart;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The mix diff's rendering rules. The diff arithmetic is pinned in
///     GetMixDiffHandlerTests; these cover what the page does with the answer — above all
///     that every lookup outcome, including "nothing moved", says something out loud.
/// </summary>
public sealed class MixChangesPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _moved = Guid.NewGuid();
    private readonly Guid _still = Guid.NewGuid();

    public MixChangesPageTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix2);
        Services.AddSingleton(settings.Object);
        Services.AddSingleton(_mediator.Object);
        this.RenderInteractive();
    }

    private static Chart Make(Guid id, string song, ChartType type, int level, MixEnum mix)
    {
        return new Chart(id, MixEnum.Phoenix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Doin", Bpm.From(180, 180)),
            type, level, mix, null, 1000, new HashSet<Skill>());
    }

    private void Catalog(MixEnum mix, params Chart[] charts)
    {
        _mediator.Setup(m => m.Send(It.Is<GetChartsQuery>(q => q.Mix == mix), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private void Diff(MixDiffRecord diff)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMixDiffQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(diff);
    }

    /// <summary>Iolite Sky: D20 became D21, and one chart that stayed put.</summary>
    private (Chart BeforeMoved, Chart AfterMoved, Chart Unchanged) SetupIoliteSky()
    {
        var beforeMoved = Make(_moved, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix);
        var afterMoved = Make(_moved, "Iolite Sky", ChartType.Double, 21, MixEnum.Phoenix2);
        var stillBefore = Make(_still, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix);
        var stillAfter = Make(_still, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix2);
        Catalog(MixEnum.Phoenix, beforeMoved, stillBefore);
        Catalog(MixEnum.Phoenix2, afterMoved, stillAfter);
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[] { new MixDiffMoveRecord(beforeMoved, afterMoved) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));
        return (beforeMoved, afterMoved, stillAfter);
    }

    private IRenderedComponent<MixChanges> RenderPage()
    {
        return RenderComponent<MixChanges>(p => p
            .Add(c => c.FromSlug, "phoenix")
            .Add(c => c.ToSlug, "phoenix-2"));
    }

    [Fact]
    public void OnlyTheSelectedFolderIsOnScreen()
    {
        var otherBefore = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 12, MixEnum.Phoenix);
        var otherAfter = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 13, MixEnum.Phoenix2);
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[] { new MixDiffMoveRecord(beforeMoved, afterMoved), new MixDiffMoveRecord(otherBefore, otherAfter) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();

        // Two folders changed; one is on screen, with only its own row.
        Assert.Single(page.FindAll(".mc-fgroup"));
        Assert.Single(page.FindAll(".mc-row"));
        Assert.Contains("folder", page.Find(".mc-fhead").TextContent);
    }

    [Fact]
    public void TheFolderInViewShowsItsChartsWithTheirMove()
    {
        SetupIoliteSky();

        var page = RenderPage();

        Assert.Contains("D20 folder", page.Find(".mc-fhead").TextContent);
        Assert.Equal("Iolite Sky", page.Find(".mc-row .mc-name").TextContent.Trim());
        Assert.Contains("▲1", page.Find(".mc-row .mc-delta").TextContent);
    }

    [Fact]
    public void ThePageOpensOnTheFolderWithTheMostChanges()
    {
        // Two D21 rerates against one D20: landing on the busiest folder means the first
        // view is never one lonely row from the bottom of the level range.
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        var busy = Enumerable.Range(0, 2).Select(i =>
        {
            var id = Guid.NewGuid();
            return new MixDiffMoveRecord(
                Make(id, $"Busy {i}", ChartType.Double, 21, MixEnum.Phoenix),
                Make(id, $"Busy {i}", ChartType.Double, 22, MixEnum.Phoenix2));
        }).ToArray();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            busy.Append(new MixDiffMoveRecord(beforeMoved, afterMoved)).ToArray(),
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();

        Assert.Contains("D21 folder", page.Find(".mc-fhead").TextContent);
        Assert.Equal(2, page.FindAll(".mc-row").Count);
    }

    [Fact]
    public void OnlyFoldersSomethingMovedOutOfArePickable()
    {
        // The predicate is the contract; FolderGridTests covers what the grid does with it.
        SetupIoliteSky();

        var page = RenderPage();
        var picker = page.FindComponent<FolderPicker>().Instance;

        Assert.True(picker.IsEnabled(ChartType.Double, 20));
        Assert.False(picker.IsEnabled(ChartType.Double, 19));
        Assert.False(picker.IsEnabled(ChartType.Single, 16));
        // Co-op charts have no level to move between, so the page hides that tab entirely.
        Assert.False(picker.ShowCoOp);
    }

    [Fact]
    public void PickingAFolderSwapsWhichOneIsOnScreen()
    {
        var id = Guid.NewGuid();
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[]
            {
                new MixDiffMoveRecord(beforeMoved, afterMoved),
                new MixDiffMoveRecord(Make(id, "Conflict", ChartType.Double, 12, MixEnum.Phoenix),
                    Make(id, "Conflict", ChartType.Double, 13, MixEnum.Phoenix2))
            },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();
        var picker = page.FindComponent<FolderPicker>();
        page.InvokeAsync(() => picker.Instance.FolderChanged.InvokeAsync((ChartType.Double, 12)))
            .GetAwaiter().GetResult();

        Assert.Contains("D12 folder", page.Find(".mc-fhead").TextContent);
        Assert.Equal("Conflict", page.Find(".mc-row .mc-name").TextContent.Trim());
    }

    [Fact]
    public void TheDirectionFilterKeepsOnlyOneSideOfTheRamp()
    {
        var id = Guid.NewGuid();
        var easierBefore = Make(id, "About The Universe", ChartType.Double, 20, MixEnum.Phoenix);
        var easierAfter = Make(id, "About The Universe", ChartType.Double, 19, MixEnum.Phoenix2);
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[] { new MixDiffMoveRecord(beforeMoved, afterMoved), new MixDiffMoveRecord(easierBefore, easierAfter) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        // Both left D20, so both are in the folder in view.
        var page = RenderPage();
        Assert.Equal(2, page.FindAll(".mc-row").Count);

        page.FindAll(".mc-chip").First(c => c.TextContent.Contains("Easier")).Click();

        var rows = page.FindAll(".mc-row");
        Assert.Single(rows);
        Assert.Contains("About The Universe", rows[0].TextContent);
        Assert.Contains("▼1", rows[0].TextContent);
    }

    [Fact]
    public void ClickingARowAnswersForThatChart()
    {
        SetupIoliteSky();

        var page = RenderPage();
        page.Find(".mc-row").Click();

        Assert.Contains("Moved up — D20 is now D21.", page.Find(".mc-verdict").TextContent);
    }

    [Fact]
    public void AChartThatDidNotMoveStillGetsAnAnswer()
    {
        // The whole point of the lookup: "nothing happened to it" is information, and the
        // old page could only express it as absence from three lists.
        var (_, _, unchanged) = SetupIoliteSky();

        var model = MixChangesAnswerModel.For(unchanged,
            new Dictionary<Name, Chart[]>
                { ["Iolite Sky"] = new[] { Make(_still, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix) } },
            new Dictionary<Name, Chart[]> { ["Iolite Sky"] = new[] { unchanged } });

        Assert.Equal(MixChangeVerdict.Unchanged, model.Verdict);
    }

    [Fact]
    public void ComparingAMixWithItselfSaysSoInsteadOfRenderingAnEmptyBoard()
    {
        Catalog(MixEnum.Phoenix2);
        Diff(MixDiffRecord.Empty(MixEnum.Phoenix2, MixEnum.Phoenix2));

        var page = RenderComponent<MixChanges>(p => p
            .Add(c => c.FromSlug, "phoenix-2")
            .Add(c => c.ToSlug, "phoenix-2"));

        Assert.Contains("Pick two different mixes.", page.Find(".mc-empty").TextContent);
    }

    [Fact]
    public void APairWithNoChangesSaysSoRatherThanShowingEmptyTabs()
    {
        Catalog(MixEnum.Phoenix);
        Catalog(MixEnum.Phoenix2);
        Diff(MixDiffRecord.Empty(MixEnum.Phoenix, MixEnum.Phoenix2));

        var page = RenderPage();

        Assert.Contains("Nothing changed between these two mixes.", page.Find(".mc-empty").TextContent);
        Assert.Empty(page.FindAll(".mc-tab"));
    }

    [Fact]
    public void ArrivalsRenderAsASheetOfSongsWithTheirCharts()
    {
        var arrival = Make(Guid.NewGuid(), "Freedom Dive", ChartType.Single, 22, MixEnum.Phoenix2);
        Catalog(MixEnum.Phoenix);
        Catalog(MixEnum.Phoenix2, arrival);
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2, Array.Empty<MixDiffMoveRecord>(),
            new[] { new MixDiffSongRecord(arrival.Song, new[] { arrival }) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();
        page.FindAll(".mc-tab")[1].Click();

        Assert.Equal("Freedom Dive", page.Find(".mc-sticker-name").TextContent.Trim());
        Assert.DoesNotContain("mc-sticker-gone", page.Find(".mc-sticker").ClassName);
    }

    [Fact]
    public void DeparturesRenderDimmedAndSayWhereTheScoresWent()
    {
        var departure = Make(Guid.NewGuid(), "Nxde", ChartType.Single, 17, MixEnum.Phoenix);
        Catalog(MixEnum.Phoenix, departure);
        Catalog(MixEnum.Phoenix2);
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2, Array.Empty<MixDiffMoveRecord>(),
            Array.Empty<MixDiffSongRecord>(),
            new[] { new MixDiffSongRecord(departure.Song, new[] { departure }) },
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();
        page.FindAll(".mc-tab")[2].Click();

        Assert.Contains("mc-sticker-gone", page.Find(".mc-sticker").ClassName);
        Assert.Contains("stay on your profile", page.Find(".mc-sheet .mud-alert").TextContent);
    }

    [Fact]
    public void GroupingByDestinationRelabelsTheFolderAndItsCount()
    {
        SetupIoliteSky();

        var page = RenderPage();
        Assert.Contains("D20 folder", page.Find(".mc-fhead").TextContent);
        Assert.Contains("1 left", page.Find(".mc-fcount").TextContent);

        page.FindAll(".mc-seg button")[1].Click();

        Assert.Contains("D21 folder", page.Find(".mc-fhead").TextContent);
        Assert.Contains("1 arrived", page.Find(".mc-fcount").TextContent);
    }

    [Fact]
    public void PickingAChartFromTheSearchAnswersForIt()
    {
        // ChartSelector hands back a plain Func, not an EventCallback, so nothing re-renders
        // on its own. This is the path that was silently dead: the row click worked because
        // it is the page's own @onclick.
        var (_, afterMoved, _) = SetupIoliteSky();

        var page = RenderPage();
        var selector = page.FindComponent<ChartSelector>();
        page.InvokeAsync(() => selector.Instance.ChartIdSelected(afterMoved)).GetAwaiter().GetResult();

        Assert.Contains("Moved up — D20 is now D21.", page.Find(".mc-verdict").TextContent);
    }

    [Fact]
    public void TheSearchIsFedBothCatalogsAndLabelsEachSuggestionWithItsMix()
    {
        // A departed song is only in the earlier catalog, and a rerated chart appears once
        // per mix under different difficulties — the mix suffix is what tells them apart.
        SetupIoliteSky();

        var page = RenderPage();
        var selector = page.FindComponent<ChartSelector>();

        Assert.True(selector.Instance.ShowMix);
        var names = selector.Instance.Charts!.Select(c => $"{c.Song.Name} {c.DifficultyString} {c.Mix}").ToArray();
        Assert.Contains("Iolite Sky D20 Phoenix", names);
        Assert.Contains("Iolite Sky D21 Phoenix2", names);
    }
}
