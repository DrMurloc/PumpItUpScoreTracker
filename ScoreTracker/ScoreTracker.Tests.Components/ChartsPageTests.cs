using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Application.Queries;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages;
using ScoreTracker.Web.Services;
using ScoreTracker.Web.Services.Contracts;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The rebuilt /Charts SRP (docs/design/charts-srp.md): the page stack renders header →
///     query chips → answer line → cards, every filter lives behind the funnel, and cards
///     are links to the chart page. The search itself is the handler's job — these facts
///     pin the page's dispatch and chip language.
/// </summary>
public sealed class ChartsPageTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUiSettingsAccessor> _uiSettings = new();
    private SearchChartsQuery? _lastQuery;

    public ChartsPageTests()
    {
        _uiSettings.Setup(u => u.GetSelectedMix(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MixEnum.Phoenix);
        _uiSettings.Setup(u => u.GetSetting(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<Guid?>()))
            .ReturnsAsync((string?)null);
        Services.AddSingleton(_uiSettings.Object);

        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.ChartIntelligence.Contracts.Queries.GetChartScoringLevelsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        _mediator.Setup(m => m.Send(It.IsAny<SearchChartsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ChartSearchResultPage>, CancellationToken>((q, _) => _lastQuery = (SearchChartsQuery)q)
            .ReturnsAsync(() => new ChartSearchResultPage(
                new[] { MakeResult("District 1", 21), MakeResult("Bee", 23) }, 2));
        _mediator.Setup(m => m.Send(It.IsAny<GetSavedChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SavedChartRecord>());
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchBadgesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChartBadge>());
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchArtistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchStepArtistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        Services.AddSingleton(_mediator.Object);
        Services.AddScoped<ChartScoringLevels>();
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>());
        Services.AddLogging();

        // DifficultyBubble branches on RendererInfo (the MudTooltip static-SSR gate).
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));

        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
    }

    private void SignIn()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(
            Guid.NewGuid(), "Tester", true, null, new Uri("https://piu.test/avatar.png"), null));
    }

    internal static ChartSearchResult MakeResult(string song, int level,
        MixEnum mix = MixEnum.Phoenix, ChartSearchMyState? my = null,
        IReadOnlyList<ChartBadge>? badges = null, TierListCategory? communityVote = null,
        TierListCategory? passDifficulty = null)
    {
        var chart = new Chart(Guid.NewGuid(), mix,
            new Song(song, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromSeconds(125), "BanYa", Bpm.From(160, 160)),
            ChartType.Double, level, mix, null, 700, new HashSet<Skill>());
        return new ChartSearchResult(chart,
            new[] { new ChartMixAppearance(mix, level, null) },
            mix, mix, null,
            badges ?? Array.Empty<ChartBadge>(), 11.2m,
            passDifficulty, null, communityVote, 21.4, null, 40, 25, 2, my);
    }

    [Fact]
    public void TheStackRendersHeaderCountAndCards()
    {
        var cut = RenderComponent<Charts>();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("2 charts", cut.Markup);
            Assert.Equal(2, cut.FindAll(".srp-card").Count);
        });
        // The scope mode sits in the page header; there is no search input anywhere.
        Assert.Contains("All Mixes", cut.Markup);
        Assert.DoesNotContain("srp-search", cut.Markup);
    }

    [Fact]
    public void CardsAreLinksToTheChartPage()
    {
        var cut = RenderComponent<Charts>();

        cut.WaitForAssertion(() =>
        {
            var links = cut.FindAll(".srp-card-link");
            Assert.Equal(2, links.Count);
            Assert.All(links, l => Assert.StartsWith("/Charts/phoenix/", l.GetAttribute("href")));
        });
    }

    [Fact]
    public void ASongNameFilterBecomesAChipCountsOnTheFunnelAndRequeries()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));

        cut.Find("button[aria-label=Filters]").Click();
        var songInput = cut.FindAll(".srp-drawer input")[0];
        songInput.Change("bee");

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(_lastQuery);
            Assert.Equal("bee", _lastQuery!.SongNameContains);
            Assert.Contains("Song name", cut.Find(".srp-chip-row").TextContent);
        });
        JSInterop.VerifyInvoke("history.pushState");
    }

    [Fact]
    public void ClearAllReturnsToTheBareQuery()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));
        cut.Find("button[aria-label=Filters]").Click();
        var inputs = cut.FindAll(".srp-drawer input");
        inputs[0].Change("bee");
        cut.WaitForAssertion(() => Assert.Equal("bee", _lastQuery!.SongNameContains));
        cut.FindAll(".srp-drawer input")[1].Change("19");
        cut.WaitForAssertion(() => Assert.Equal(19, _lastQuery!.LevelMin));

        var clearAll = cut.FindAll("button").Single(b => b.TextContent.Trim() == "Clear all");
        clearAll.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Null(_lastQuery!.SongNameContains);
            Assert.Null(_lastQuery.LevelMin);
        });
    }

    [Fact]
    public void AnonymousVisitorsGetNoScoreStateFilterOrMyOverlay()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));

        cut.Find("button[aria-label=Filters]").Click();

        Assert.DoesNotContain("Score State", cut.Markup);
        Assert.Empty(cut.FindAll(".srp-card-my"));
        Assert.Null(_lastQuery!.UserId);
    }

    [Fact]
    public void SignedInVisitorsSearchAsThemselves()
    {
        SignIn();

        var cut = RenderComponent<Charts>();

        cut.WaitForAssertion(() =>
        {
            Assert.NotNull(_lastQuery?.UserId);
            Assert.NotEmpty(cut.FindAll(".srp-card-my"));
        });
    }

    [Fact]
    public void TheBadgeCloudTogglesGranularBadgesWithResultCounts()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchBadgesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartBadge("staggered_bracket", "Staggered Brackets", SkillCategory.Bracket)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchArtistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mediator.Setup(m => m.Send(It.IsAny<GetSearchStepArtistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<string>());
        _mediator.Setup(m => m.Send(It.IsAny<SearchChartsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<ChartSearchResultPage>, CancellationToken>((q, _) => _lastQuery = (SearchChartsQuery)q)
            .ReturnsAsync(() => new ChartSearchResultPage(new[] { MakeResult("District 1", 21) }, 1,
                new ChartSearchFacetCounts(
                    new Dictionary<ChartType, int>(),
                    new Dictionary<SongType, int>(),
                    new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["staggered_bracket"] = 7 },
                    new Dictionary<TierListCategory, int>(),
                    new Dictionary<TierListCategory, int>(),
                    new Dictionary<TierListCategory, int>())));
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".srp-card")));

        cut.Find("button[aria-label=Filters]").Click();
        cut.WaitForAssertion(() => Assert.Contains("Staggered Brackets (7)", cut.Markup));

        cut.Find(".srp-badge-opt").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(new[] { "staggered_bracket" }, _lastQuery!.Badges);
            Assert.Contains("Staggered Brackets", cut.Find(".srp-chip-row").TextContent);
        });
    }

    [Fact]
    public void CommunityVoteAndMixScopeGroupsOnlyActivateWhenLegacyIsInScope()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));
        cut.Find("button[aria-label=Filters]").Click();

        // Pure Phoenix scope: no vote facet, no mix-scope group.
        Assert.DoesNotContain("Community Vote", cut.Markup);
        Assert.DoesNotContain("Rerated up", cut.Markup);

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "All Mixes").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Community Vote", cut.Markup);
            Assert.Contains("Rerated up", cut.Markup);
        });
    }

    [Fact]
    public void DisplaySwitchesPersistWithoutRefiltering()
    {
        SignIn();
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));
        var sendsBefore = _mediator.Invocations.Count(i =>
            i.Arguments.FirstOrDefault() is SearchChartsQuery);

        cut.Find("button[aria-label=Filters]").Click();
        var stepArtistSwitch = cut.FindAll("input[type=checkbox]")
            .First(i => i.Closest("label")!.TextContent.Contains("Step Artist"));
        stepArtistSwitch.Change(true);

        cut.WaitForAssertion(() => _uiSettings.Verify(u =>
            u.SetSetting("Charts__Display__StepArtist", true.ToString(), It.IsAny<CancellationToken>()), Times.Once));
        Assert.Equal(sendsBefore, _mediator.Invocations.Count(i =>
            i.Arguments.FirstOrDefault() is SearchChartsQuery));
    }

    [Fact]
    public void DensityCyclesCardsStickersAndTableAndPersists()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));

        cut.Find("button[aria-label=Compact]").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindAll(".tier-chart-card-compact").Count);
            Assert.Empty(cut.FindAll(".srp-card"));
        });
        _uiSettings.Verify(u => u.SetSetting("Density__Charts", "Compact", It.IsAny<CancellationToken>()),
            Times.Once);
        // Stickers are links too — the tooltip carries identity, the href carries the page.
        Assert.All(cut.FindAll("a.tier-chart-card"),
            a => Assert.StartsWith("/Charts/phoenix/", a.GetAttribute("href")));

        cut.Find("button[aria-label=Table]").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".srp-table")));
    }

    [Fact]
    public void TableHeadersCarryTheSortAndTheSortChipRetires()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));
        cut.Find("button[aria-label=Table]").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".srp-table")));

        var npsHeader = cut.FindAll("th").Single(h => h.TextContent.Trim().StartsWith("NPS"));
        npsHeader.Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(ChartSearchSort.Nps, _lastQuery!.Sort);
            var sorted = cut.Find(".srp-th-sorted");
            Assert.StartsWith("NPS", sorted.TextContent.Trim());
        });
        // The header shows the sort; no ⇅ chip joins the query row in Table density.
        Assert.DoesNotContain("⇅", cut.Find(".srp-chip-row").TextContent);
    }

    [Fact]
    public void TheScopeToggleWidensToAllMixesWithoutBecomingAChip()
    {
        var cut = RenderComponent<Charts>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".srp-card").Count));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "All Mixes").Click();

        cut.WaitForAssertion(() => Assert.True(_lastQuery!.AllMixes));
        Assert.DoesNotContain("All Mixes", cut.Find(".srp-chip-row").TextContent);
    }
}
