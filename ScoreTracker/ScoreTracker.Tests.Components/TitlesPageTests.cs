using AngleSharp.Dom;
using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles;
using ScoreTracker.Domain.Models.Titles.Phoenix;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Pages.Progress;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The rebuilt /Titles page. Rendered against the real Phoenix title list, because the
///     shapes worth asserting on — rails that outnumber their section headers, titles the
///     import owns, a rail whose rungs are not in requirement order — are properties of that
///     list rather than of a fixture.
/// </summary>
public sealed class TitlesPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();

    public TitlesPageTests()
    {
        var user = new User(Guid.NewGuid(), "DrMurloc", true, "DrMurloc",
            new Uri("https://piu.test/p.png"), "US");
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(user);

        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSelectedMix()).ReturnsAsync(MixEnum.Phoenix);
        Services.AddSingleton(settings.Object);
        Services.AddSingleton(_mediator.Object);

        WithTitles(new HashSet<Name>());
        WithRarity(new Dictionary<Name, int>(), 1562);
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleHoldersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleHoldersRecord(Array.Empty<TitleHolder>(), 0));
    }

    private void WithTitles(ISet<Name> completed)
    {
        var progress = PhoenixTitleList.BuildProgress(
            new Dictionary<Guid, SharedKernel.Models.Chart>(),
            Array.Empty<RecordedPhoenixScore>(), completed);
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleProgressQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(progress);
    }

    private void WithRarity(IReadOnlyDictionary<Name, int> holders, int tracked)
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetTitleRarityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TitleRarityRecord(holders, tracked));
    }

    private IRenderedComponent<Titles> Render()
    {
        return RenderComponent<Titles>();
    }

    [Fact]
    public void DrawsOneRailPerLadderRatherThanOneRowPerTitle()
    {
        var page = Render();

        // 213 titles, 47 rails. The whole point of the overhaul.
        Assert.Equal(47, page.FindAll(".title-rail").Count);
        Assert.Equal(213, page.FindAll(".title-pip").Count + page.FindAll(".title-badge").Count);
    }

    [Fact]
    public void EverySectionThatRendersHasSomethingUnderIt()
    {
        var page = Render();
        var sections = page.FindAll(".title-section");

        Assert.NotEmpty(sections);
        Assert.All(sections, s =>
            Assert.True(s.QuerySelectorAll(".title-rail").Length > 0 ||
                        s.QuerySelectorAll(".title-badge").Length > 0));
    }

    [Fact]
    public void ATitleOnlyTheImportCanAwardIsMarkedAndCarriesNoFill()
    {
        var page = Render();

        // 77 of Phoenix's titles have no requirement behind them.
        var official = page.FindAll(".title-pip.official").Count + page.FindAll(".title-badge.official").Count;
        Assert.Equal(77, official);
        Assert.All(page.FindAll(".title-pip.official"), pip =>
            Assert.DoesNotContain("active", pip.ClassList));
    }

    [Fact]
    public void AnUnearnedComputedRungIsNeverMarkedOfficial()
    {
        var page = Render();
        // The difficulty ladders compute end to end, so nothing in Progression is dashed.
        var progression = page.FindAll(".title-section")[0];
        Assert.Empty(progression.QuerySelectorAll(".title-pip.official"));
    }

    [Fact]
    public void TheStandingBarNamesTheTitleYouWearAndCountsWhatYouHold()
    {
        WithTitles(new HashSet<Name> { "Intermediate Lv. 1", "Intermediate Lv. 2", "GOLD MEMBER" });
        var page = Render();

        Assert.Contains("Intermediate Lv. 2", page.Find(".title-worn").TextContent);
        Assert.Contains("3 / 213", page.Find(".title-tally-num").TextContent);
    }

    [Fact]
    public void ASignedOutVisitorSeesTheCatalogueAndAnInvitationRatherThanProgress()
    {
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(false);
        var page = Render();

        Assert.Empty(page.FindAll(".title-standing"));
        Assert.Single(page.FindAll(".title-anon"));
        // The rails still render — the list is worth browsing signed out.
        Assert.Equal(47, page.FindAll(".title-rail").Count);
    }

    [Fact]
    public void FilteringToEarnedHidesEveryRailWithNothingEarnedOnIt()
    {
        WithTitles(new HashSet<Name> { "Intermediate Lv. 1" });
        var page = Render();

        page.FindAll(".title-chip")[2].Click();

        // One rail survives, and it keeps its whole ladder — only the rungs that do not
        // match fade, so you can still see where on the climb your earned rung sits.
        Assert.Single(page.FindAll(".title-rail"));
        Assert.Equal(10, page.FindAll(".title-pip").Count);
        Assert.Equal(9, page.FindAll(".title-pip.dim").Count);
    }

    [Fact]
    public void SearchingMatchesTheChartATitleAsksForNotJustItsName()
    {
        var page = Render();

        page.Find(".title-search input").Input("Sorceress Elise");

        // The field debounces, so the filter lands a beat after the keystroke.
        page.WaitForAssertion(() =>
        {
            var lit = page.FindAll(".title-pip:not(.dim)");
            Assert.Single(lit);
            Assert.Equal("[DRILL] Lv.7", lit[0].GetAttribute("title"));
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ASearchThatMatchesNothingSaysSoInsteadOfRenderingEmptySections()
    {
        var page = Render();

        page.Find(".title-search input").Input("zzzzzz");

        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".title-rail"));
            Assert.Empty(page.FindAll(".title-badge"));
            Assert.Contains("No titles match", page.Find(".title-empty").TextContent);
        }, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ClickingARungOpensItsDetailAndAsksForItsHolders()
    {
        var page = Render();

        page.FindAll(".title-pip")[0].Click();

        Assert.Contains("Intermediate Lv. 1", page.Find(".title-drawer-head h3").TextContent);
        _mediator.Verify(m => m.Send(
            It.Is<GetTitleHoldersQuery>(q => q.Title == (Name)"Intermediate Lv. 1" && q.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ClosingTheDrawerStaysClosedWhenTheRestOfThePageRerenders()
    {
        // Field test: dismissing the drawer left the selected rung set, so the next render of
        // anything else on the page opened it straight back up. The selection is now the only
        // open state there is.
        var page = Render();
        page.FindAll(".title-pip")[0].Click();
        Assert.NotEmpty(page.FindAll(".title-drawer"));

        page.Find(".title-drawer-close").Click();
        Assert.Empty(page.FindAll(".title-drawer"));

        page.FindAll(".title-chip")[1].Click();

        Assert.Empty(page.FindAll(".title-drawer"));
    }

    [Fact]
    public void DismissingTheDrawerAnyOtherWayAlsoClearsTheSelection()
    {
        // The scrim and Escape come back through MudDrawer's OpenChanged rather than through
        // the close button, and that was the path that used to leave the selection behind.
        var page = Render();
        page.FindAll(".title-pip")[0].Click();

        var drawer = page.FindComponent<MudBlazor.MudDrawer>();
        page.InvokeAsync(() => drawer.Instance.OpenChanged.InvokeAsync(false)).GetAwaiter().GetResult();

        Assert.Empty(page.FindAll(".title-drawer"));
    }

    [Fact]
    public void TheDetailOfAnImportOnlyTitleExplainsItselfInsteadOfShowingABar()
    {
        var page = Render();

        page.FindAll(".title-pip.official")[0].Click();

        Assert.Single(page.FindAll(".title-note"));
        Assert.Empty(page.FindAll(".title-bar"));
    }

    [Fact]
    public void TheDetailOfAComputedTitleShowsTheClimbFromTheRungBelowIt()
    {
        var page = Render();

        // Advanced Lv. 2 measures from Lv. 1's 13,000 rather than from zero.
        var pip = page.FindAll(".title-pip").Single(p => p.GetAttribute("title") == "Advanced Lv. 2");
        pip.Click();

        Assert.Single(page.FindAll(".title-bar"));
        Assert.Contains("13,000", page.Find(".title-figures").TextContent);
    }

    [Fact]
    public void RarityIsPrintedBesideItsColourRatherThanBeingColourAlone()
    {
        WithRarity(new Dictionary<Name, int> { [(Name)"The Master"] = 8 }, 1562);
        var page = Render();

        page.FindAll(".title-pip").Single(p => p.GetAttribute("title") == "The Master").Click();

        Assert.Contains("0.5%", page.Find(".title-rarity-pct").TextContent);
        Assert.Contains("8 of 1,562", page.Find(".title-rarity .title-hint").TextContent);
    }

    [Fact]
    public void PhoenixHasNoSuggestedLevelBecauseThatReadIsPhoenixTwoOnly()
    {
        var page = Render();

        page.FindAll(".title-pip")[0].Click();

        Assert.Empty(page.FindAll(".title-suggest"));
    }

    // ---- chart titles (docs/design/titles-overhaul.md, chart titles) -------------------------

    private static readonly Chart XxSingleBoss = TitleChart("ERRORCODE: 0", "https://piu.test/errorcode.png",
        ChartType.Single, 25);

    private static readonly Chart DrillSeven = TitleChart("Sorceress Elise", "https://piu.test/elise.png",
        ChartType.Single, 21);

    private static Chart TitleChart(string song, string art, ChartType type, int level) =>
        new(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(song, SongType.Arcade, new Uri(art), TimeSpan.FromMinutes(2), "Artist", Bpm.From(200, 200)),
            type, level, MixEnum.Phoenix, null, 900);

    private void WithCharts(params Chart[] charts) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);

    private void WithBest(params RecordedPhoenixScore[] records) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

    private static IElement Pip(IRenderedComponent<Titles> page, string title) =>
        page.FindAll(".title-pip").Single(p => p.GetAttribute("title") == title);

    /// <summary>
    ///     A boss breaker names one chart, so its drawer draws the chart as the site's card where it
    ///     used to print a sentence — and drops the 0 / 1 bar, which a title one result decides
    ///     could never move past either end of.
    /// </summary>
    [Fact]
    public async Task AChartTitleShowsItsChartAsACardInsteadOfASentence()
    {
        WithCharts(XxSingleBoss);
        var page = Render();

        await Pip(page, "[XX] Single Boss breaker").ClickAsync(new MouseEventArgs());

        Assert.Single(page.FindAll(".title-drawer .tier-chart-card"));
        Assert.Empty(page.FindAll(".title-drawer .title-req"));
        Assert.Contains("Any clear", page.Find(".title-chart-req").TextContent);
        Assert.Empty(page.FindAll(".title-bar"));
    }

    [Fact]
    public async Task TheCardCarriesYourScoreTheWayEveryChartCardDoes()
    {
        WithCharts(XxSingleBoss);
        WithBest(new RecordedPhoenixScore(XxSingleBoss.Id, 951_207, PhoenixPlate.TalentedGame, false,
            DateTimeOffset.UnixEpoch));
        // The score line asks which world it renders in; /Titles is a circuit page.
        this.RenderInteractive();
        var page = Render();

        await Pip(page, "[XX] Single Boss breaker").ClickAsync(new MouseEventArgs());

        Assert.Single(page.FindAll(".title-drawer .tier-chart-card.tier-chart-card-pass"));
        Assert.Single(page.FindAll(".title-drawer .tier-chart-card-scoreline"));
    }

    /// <summary>
    ///     A skill rung asks for a grade, printed as the grade's own picture. Its bar stays: 900,000
    ///     to 990,000 is a climb the bar actually measures.
    /// </summary>
    [Fact]
    public async Task ASkillRungAsksForItsGradeAndKeepsItsClimb()
    {
        WithCharts(DrillSeven);
        var page = Render();

        await Pip(page, "[DRILL] Lv.7").ClickAsync(new MouseEventArgs());

        var ask = page.Find(".title-chart-req");
        Assert.Contains("or better", ask.TextContent);
        Assert.Single(ask.QuerySelectorAll("img"));
        Assert.Single(page.FindAll(".title-bar"));
    }

    /// <summary>
    ///     Chart Details opens over the drawer, and the drawer stays where it was underneath — closing
    ///     the dialog lands back on the title the player was reading.
    /// </summary>
    [Fact]
    public async Task TappingTheCardOpensChartDetailsOverTheDrawer()
    {
        WithCharts(XxSingleBoss);
        var page = Render();
        await Pip(page, "[XX] Single Boss breaker").ClickAsync(new MouseEventArgs());

        await page.Find(".title-drawer .tier-chart-card-jacket").ClickAsync(new MouseEventArgs());

        var dialog = page.FindComponent<ChartDetailsDialog>().Instance;
        Assert.True(dialog.Visible);
        Assert.Equal(XxSingleBoss.Id, dialog.Chart?.Id);
        Assert.NotEmpty(page.FindAll(".title-drawer"));
    }

    /// <summary>A song that has not imported yet leaves the title as it was: its sentence, no card.</summary>
    [Fact]
    public async Task ATitleWhoseChartIsNotInTheCatalogKeepsItsSentence()
    {
        var page = Render();

        await Pip(page, "[XX] Single Boss breaker").ClickAsync(new MouseEventArgs());

        Assert.Single(page.FindAll(".title-drawer .title-req"));
        Assert.Empty(page.FindAll(".title-drawer .tier-chart-card"));
    }

    /// <summary>
    ///     A boss cell IS a chart, so it carries the jacket and stepball and names which of the era's
    ///     two it is in one word. A numbered skill rail stays numbered.
    /// </summary>
    [Fact]
    public void BossCellsCarryTheirChartAndSkillRailsStayNumbered()
    {
        WithCharts(XxSingleBoss, DrillSeven);
        var page = Render();

        var boss = Pip(page, "[XX] Single Boss breaker");
        Assert.Contains("with-art", boss.ClassList);
        Assert.Equal("https://piu.test/errorcode.png", boss.QuerySelector("img.title-pip-jacket")?.GetAttribute("src"));
        Assert.Equal("Single", boss.QuerySelector(":scope > span")?.TextContent.Trim());

        Assert.DoesNotContain("with-art", Pip(page, "[DRILL] Lv.7").ClassList);
    }
}
