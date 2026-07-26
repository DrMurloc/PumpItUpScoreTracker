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
public sealed class WhatChangedPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Guid _moved = Guid.NewGuid();
    private readonly Guid _still = Guid.NewGuid();

    public WhatChangedPageTests()
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

    private IRenderedComponent<WhatChanged> RenderPage()
    {
        return RenderComponent<WhatChanged>(p => p
            .Add(c => c.FromSlug, "phoenix")
            .Add(c => c.ToSlug, "phoenix-2"));
    }

    [Fact]
    public void TheBoardGroupsReRatesByTheFolderTheyLeft()
    {
        SetupIoliteSky();

        var page = RenderPage();

        Assert.Contains("D20 folder", page.Find(".wc-fhead").TextContent);
        Assert.Equal("Iolite Sky", page.Find(".wc-row .wc-name").TextContent.Trim());
        Assert.Contains("▲1", page.Find(".wc-row .wc-delta").TextContent);
    }

    [Fact]
    public void TappingAFolderHeaderNarrowsToThatFolderAndTappingItAgainReleases()
    {
        var otherBefore = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 12, MixEnum.Phoenix);
        var otherAfter = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 13, MixEnum.Phoenix2);
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[] { new MixDiffMoveRecord(beforeMoved, afterMoved), new MixDiffMoveRecord(otherBefore, otherAfter) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();
        Assert.Equal(2, page.FindAll(".wc-fgroup").Count);

        page.FindAll(".wc-fhead")[0].Click();
        Assert.Single(page.FindAll(".wc-fgroup"));

        page.Find(".wc-fhead").Click();
        Assert.Equal(2, page.FindAll(".wc-fgroup").Count);
    }

    [Fact]
    public void TheDirectionFilterKeepsOnlyOneSideOfTheRamp()
    {
        var easierBefore = Make(Guid.NewGuid(), "About The Universe", ChartType.Single, 21, MixEnum.Phoenix);
        var easierAfter = Make(Guid.NewGuid(), "About The Universe", ChartType.Single, 20, MixEnum.Phoenix2);
        var (beforeMoved, afterMoved, _) = SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2,
            new[] { new MixDiffMoveRecord(beforeMoved, afterMoved), new MixDiffMoveRecord(easierBefore, easierAfter) },
            Array.Empty<MixDiffSongRecord>(), Array.Empty<MixDiffSongRecord>(),
            Array.Empty<Chart>(), Array.Empty<Chart>()));

        var page = RenderPage();
        Assert.Equal(2, page.FindAll(".wc-row").Count);

        page.FindAll(".wc-chip").First(c => c.TextContent.Contains("Easier")).Click();

        var rows = page.FindAll(".wc-row");
        Assert.Single(rows);
        Assert.Contains("About The Universe", rows[0].TextContent);
        Assert.Contains("▼1", rows[0].TextContent);
    }

    [Fact]
    public void ClickingARowAnswersForThatChart()
    {
        SetupIoliteSky();

        var page = RenderPage();
        page.Find(".wc-row").Click();

        Assert.Contains("Moved up — D20 is now D21.", page.Find(".wc-verdict").TextContent);
    }

    [Fact]
    public void AChartThatDidNotMoveStillGetsAnAnswer()
    {
        // The whole point of the lookup: "nothing happened to it" is information, and the
        // old page could only express it as absence from three lists.
        var (_, _, unchanged) = SetupIoliteSky();

        var page = RenderPage();
        page.Instance.GetType();
        var model = WhatChangedAnswerModel.For(unchanged,
            new Dictionary<Name, Chart[]>
                { ["Iolite Sky"] = new[] { Make(_still, "Iolite Sky", ChartType.Single, 16, MixEnum.Phoenix) } },
            new Dictionary<Name, Chart[]> { ["Iolite Sky"] = new[] { unchanged } });

        Assert.Equal(WhatChangedVerdict.Unchanged, model.Verdict);
    }

    [Fact]
    public void ComparingAMixWithItselfSaysSoInsteadOfRenderingAnEmptyBoard()
    {
        Catalog(MixEnum.Phoenix2);
        Diff(MixDiffRecord.Empty(MixEnum.Phoenix2, MixEnum.Phoenix2));

        var page = RenderComponent<WhatChanged>(p => p
            .Add(c => c.FromSlug, "phoenix-2")
            .Add(c => c.ToSlug, "phoenix-2"));

        Assert.Contains("Pick two different mixes.", page.Find(".wc-empty").TextContent);
    }

    [Fact]
    public void APairWithNoChangesSaysSoRatherThanShowingEmptyTabs()
    {
        Catalog(MixEnum.Phoenix);
        Catalog(MixEnum.Phoenix2);
        Diff(MixDiffRecord.Empty(MixEnum.Phoenix, MixEnum.Phoenix2));

        var page = RenderPage();

        Assert.Contains("Nothing changed between these two mixes.", page.Find(".wc-empty").TextContent);
        Assert.Empty(page.FindAll(".wc-tab"));
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
        page.FindAll(".wc-tab")[1].Click();

        Assert.Equal("Freedom Dive", page.Find(".wc-sticker-name").TextContent.Trim());
        Assert.DoesNotContain("wc-sticker-gone", page.Find(".wc-sticker").ClassName);
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
        page.FindAll(".wc-tab")[2].Click();

        Assert.Contains("wc-sticker-gone", page.Find(".wc-sticker").ClassName);
        Assert.Contains("stay on your profile", page.Find(".wc-sheet .mud-alert").TextContent);
    }

    [Fact]
    public void GroupingByDestinationRelabelsTheFolderAndItsCount()
    {
        SetupIoliteSky();

        var page = RenderPage();
        Assert.Contains("D20 folder", page.Find(".wc-fhead").TextContent);
        Assert.Contains("1 left", page.Find(".wc-fcount").TextContent);

        page.FindAll(".wc-seg button")[1].Click();

        Assert.Contains("D21 folder", page.Find(".wc-fhead").TextContent);
        Assert.Contains("1 arrived", page.Find(".wc-fcount").TextContent);
    }

    [Fact]
    public void TheLastRemainingChartTypeCannotBeSwitchedOff()
    {
        SetupIoliteSky();

        var page = RenderPage();
        var singles = page.FindAll(".wc-chip").First(c => c.TextContent.Trim() == "Singles");
        var doubles = page.FindAll(".wc-chip").First(c => c.TextContent.Trim() == "Doubles");

        singles.Click();
        page.FindAll(".wc-chip").First(c => c.TextContent.Trim() == "Doubles").Click();

        // Doubles stayed pressed: turning the last one off would leave a dead board.
        Assert.Equal("true",
            page.FindAll(".wc-chip").First(c => c.TextContent.Trim() == "Doubles").GetAttribute("aria-pressed"));
        Assert.Single(page.FindAll(".wc-row"));
    }
}
