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
using ScoreTracker.Domain.SecondaryPorts;
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
        // The sticker sheets mount ChartDetailsDialog (closed) so a chart ball can open it.
        Services.AddSingleton(Mock.Of<IAdminNotificationClient>());
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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));
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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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

        Assert.False(picker.IsMissing(ChartType.Double, 20));
        Assert.True(picker.IsMissing(ChartType.Double, 19));
        Assert.True(picker.IsMissing(ChartType.Single, 16));
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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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

        var model = MixChangesAnswerModel.For("Iolite Sky", unchanged,
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
            Array.Empty<MixDiffSongRecord>(), Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

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
    public void ResteppedChartsGetTheirOwnSectionWithBeforeAndAfterNoteCounts()
    {
        var id = Guid.NewGuid();
        var before = Make(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix);
        var after = Make(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix2) with { NoteCount = 1012 };
        Catalog(MixEnum.Phoenix, before);
        Catalog(MixEnum.Phoenix2, after);
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2, Array.Empty<MixDiffMoveRecord>(),
            Array.Empty<MixDiffSongRecord>(),
            new[] { new MixDiffSongRecord(before.Song, new[] { before }) },
            Array.Empty<Chart>(), Array.Empty<Chart>(),
            new[] { new MixDiffMoveRecord(before, after) }, true));

        var page = RenderPage();
        page.FindAll(".mc-tab").First(t => t.TextContent.Contains("Note Counts")).Click();

        var row = page.Find(".mc-sheet .mc-row");
        Assert.Contains("Iolite Sky", row.TextContent);
        Assert.Contains("1,000", row.TextContent);
        Assert.Contains("1,012", row.TextContent);
        Assert.Contains("▲12", row.TextContent);
    }

    [Fact]
    public void APairThatTracksNoteCountsButHasNoRestepsSaysSo()
    {
        SetupIoliteSky();
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2, Array.Empty<MixDiffMoveRecord>(),
            Array.Empty<MixDiffSongRecord>(),
            new[] { new MixDiffSongRecord(Make(Guid.NewGuid(), "Nxde", ChartType.Single, 17, MixEnum.Phoenix).Song,
                new[] { Make(Guid.NewGuid(), "Nxde", ChartType.Single, 17, MixEnum.Phoenix) }) },
            Array.Empty<Chart>(), Array.Empty<Chart>(),
            Array.Empty<MixDiffMoveRecord>(), true, 9));

        var page = RenderPage();
        page.FindAll(".mc-tab").First(t => t.TextContent.Contains("Note Counts")).Click();

        var notice = page.Find(".mc-sheet .mud-alert").TextContent;
        Assert.Contains("No chart's note count changed", notice);
        // "None re-stepped" must not be mistaken for "we checked everything".
        Assert.Contains("9 charts have no note count recorded", notice);
        Assert.Empty(page.FindAll(".mc-sheet .mc-row"));
    }

    [Fact]
    public void APairThatRecordsNoNoteCountsHasNoSuchTabAtAll()
    {
        // Absent, not zero: a zero would claim the question was asked and answered.
        SetupIoliteSky();

        var page = RenderPage();

        Assert.DoesNotContain(page.FindAll(".mc-tab"), t => t.TextContent.Contains("Note Counts"));
    }

    [Fact]
    public void TheAnswerCardNamesARestepEvenWhenTheLevelHeld()
    {
        var id = Guid.NewGuid();
        var before = Make(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix);
        var after = Make(id, "Iolite Sky", ChartType.Double, 20, MixEnum.Phoenix2) with { NoteCount = 1012 };

        var model = MixChangesAnswerModel.For("Iolite Sky", after,
            new Dictionary<Name, Chart[]> { ["Iolite Sky"] = new[] { before } },
            new Dictionary<Name, Chart[]> { ["Iolite Sky"] = new[] { after } });

        Assert.Equal(MixChangeVerdict.Unchanged, model.Verdict);
        Assert.Equal(12, model.PinnedNoteDelta);
    }

    [Fact]
    public void PickingASongFromTheSearchAnswersForTheWholeSong()
    {
        // The search knows the song, not the chart, so the answer summarises rather than
        // naming one chart. This whole path was silently dead once — see SongSelector's
        // EventCallback note.
        SetupIoliteSky();

        var page = RenderPage();
        var selector = page.FindComponent<SongSelector>();
        page.InvokeAsync(() => selector.Instance.SongSelected.InvokeAsync("Iolite Sky"))
            .GetAwaiter().GetResult();

        Assert.Contains("1 of 2 charts moved.", page.Find(".mc-verdict").TextContent);
    }

    [Fact]
    public void OnlySongsWithAChangedChartAreOffered()
    {
        // The answer is about a whole song, so the search is too — and offering the full
        // catalog meant thousands of suggestions that resolve to "nothing changed".
        var untouchedBefore = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 12, MixEnum.Phoenix);
        var untouchedAfter = Make(Guid.NewGuid(), "Conflict", ChartType.Single, 12, MixEnum.Phoenix2);
        var (beforeMoved, _, stillAfter) = SetupIoliteSky();
        Catalog(MixEnum.Phoenix, beforeMoved, untouchedBefore);
        Catalog(MixEnum.Phoenix2, stillAfter, untouchedAfter);

        var page = RenderPage();
        var songs = page.FindComponent<SongSelector>().Instance.Songs
            .Select(s => s.Name.ToString()).ToArray();

        Assert.Equal(new[] { "Iolite Sky" }, songs);
    }

    [Fact]
    public void ArrivalsAndDeparturesStaySearchableBecauseThatIsAlsoWhatHappenedToThem()
    {
        var arrival = Make(Guid.NewGuid(), "Freedom Dive", ChartType.Single, 22, MixEnum.Phoenix2);
        var departure = Make(Guid.NewGuid(), "Nxde", ChartType.Single, 17, MixEnum.Phoenix);
        Catalog(MixEnum.Phoenix, departure);
        Catalog(MixEnum.Phoenix2, arrival);
        Diff(new MixDiffRecord(MixEnum.Phoenix, MixEnum.Phoenix2, Array.Empty<MixDiffMoveRecord>(),
            new[] { new MixDiffSongRecord(arrival.Song, new[] { arrival }) },
            new[] { new MixDiffSongRecord(departure.Song, new[] { departure }) },
            Array.Empty<Chart>(), Array.Empty<Chart>(), Array.Empty<MixDiffMoveRecord>()));

        var page = RenderPage();
        var songs = page.FindComponent<SongSelector>().Instance.Songs
            .Select(s => s.Name.ToString()).ToArray();

        Assert.Contains("Freedom Dive", songs);
        Assert.Contains("Nxde", songs);
    }

    [Fact]
    public void UnchangedChartsAreListedOutrightRatherThanHiddenBehindACount()
    {
        SetupIoliteSky();

        var page = RenderPage();
        page.Find(".mc-row").Click();

        // Iolite Sky S16 held its level; the answer says so with its bubble, no toggle.
        Assert.Contains("Unchanged Charts:", page.Find(".mc-unchanged").TextContent);
        Assert.Single(page.FindAll(".mc-unchanged-list img"));
        Assert.DoesNotContain("Open the chart page", page.Markup);
    }

    [Fact]
    public void ThePageCarriesNoSourcingOrProvisionalDisclaimers()
    {
        // Both were retired once they stopped being true: the XX sourcing credit, and the
        // "not final until the mix releases" caveat on anything compared against Phoenix 2.
        SetupIoliteSky();

        var page = RenderPage();

        Assert.DoesNotContain("KyleTT", page.Markup);
        Assert.DoesNotContain("not final", page.Markup);
    }
}
