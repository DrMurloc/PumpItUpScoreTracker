using System.Text.RegularExpressions;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Microsoft.AspNetCore.Components;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.TierLists;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class PersonalizedBreakdownPageTests : ComponentTestBase
{
    private static readonly Guid UserId = Guid.NewGuid();

    // Deliberately not in projection order and not in score order, so neither sort can pass by
    // accident: the hardest chart for this level is one the player happens to have done well on.
    private static readonly (string Name, int Projected, int? Mine)[] Folder =
    {
        ("Hardest for your level", 905_000, 960_000),
        ("Middling", 940_000, 900_000),
        ("Easiest for your level", 975_000, 930_000),
        ("Never touched", 950_000, null)
    };

    [Fact]
    public void TheSpreadSortsByProjectionUntilToldOtherwise()
    {
        var cut = RenderPage();

        Assert.Equal(
            new[] { "Hardest for your level", "Middling", "Never touched", "Easiest for your level" },
            SpreadOrder(cut));
    }

    [Fact]
    public void SortingByMyScoresOrdersOnWhatYouDidAndParksTheUnplayedAtTheEnd()
    {
        // Ascending either way, so the top stays the hard end — by what this level scores, or by
        // what you actually did. An unplayed chart has nothing to sort on and would otherwise
        // read as a zero at the very top, which is the opposite of true: nobody has failed it.
        var cut = RenderPage("Mine");

        Assert.Equal(
            new[] { "Middling", "Easiest for your level", "Hardest for your level", "Never touched" },
            SpreadOrder(cut));
    }

    [Fact]
    public async Task ChoosingASortReordersTheSpreadAndRemembersIt()
    {
        var cut = RenderPage();
        Assert.Equal("Hardest for your level", SpreadOrder(cut)[0]);

        // Open the sort menu, then pick My Scores.
        await cut.FindAll(".mud-menu button")[0].ClickAsync(new MouseEventArgs());
        // Found by its text rather than a Mud class: the menu's item markup is the framework's
        // to change, and the label is what a reader is actually clicking.
        var items = cut.FindAll("*")
            .Where(e => e.Children.Length == 0 && e.TextContent.Trim() == "My Scores")
            .ToArray();
        var byMyScores = Assert.Single(items);
        await byMyScores.ClickAsync(new MouseEventArgs());

        Assert.Equal("Middling", SpreadOrder(cut)[0]);
        Settings().Verify(s => s.SetSetting("Breakdown__SpreadSort", "Mine",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AnUnrecognisedSavedSortFallsBackToTheProjection()
    {
        // A stored value naming a sort that no longer exists would otherwise leave the menu
        // ticking nothing while the list quietly used the default.
        var cut = RenderPage("SomethingRetired");

        Assert.Equal(
            new[] { "Hardest for your level", "Middling", "Never touched", "Easiest for your level" },
            SpreadOrder(cut));
    }

    [Fact]
    public void TheVerdictCountsTheProjectionsGapsNotTheRetiredSourcesSilence()
    {
        // Skill and Similar Players are not in the Score recipe at all, so the handler returns
        // Unrecorded for both on every chart. Keying "not enough data" on them therefore counted
        // the WHOLE folder — which drove "match the community tier" negative the moment anything
        // moved, and told the reader that charts drawn on the spread above had nobody near their
        // level scoring them.
        var cut = RenderPage(movers: true);

        // Four charts, one with no projection, two moved: 4 - 2 - 1 = 1 unchanged.
        Assert.Contains("1 not enough data", cut.Markup);
        Assert.Contains("1 match the community tier", cut.Markup);
        Assert.DoesNotContain("−1 match", cut.Markup);
        Assert.DoesNotContain("-1 match", cut.Markup);
    }

    private static string[] SpreadOrder(IRenderedFragment cut)
    {
        return Regex.Matches(cut.Markup, @"class=""spread-title[^""]*"">([^<]+)<")
            .Select(m => m.Groups[1].Value)
            .ToArray();
    }

    private Mock<IUiSettingsAccessor> Settings()
    {
        return Mock.Get((IUiSettingsAccessor)Services.GetService(typeof(IUiSettingsAccessor))!);
    }

    [Fact]
    public void OnPhoenix2TheStatsNameThePumbilityPeersAndTheirRungBand()
    {
        // The projection was drawn from PUMBILITY peers (docs/design/personalized-breakdown.md):
        // the group and its rung band replace the competitive figures, and nothing is discounted,
        // so the stale-weight figure is not printed at all.
        var cut = RenderPage(peers: PeerGroup.Pumbility(24, 23, 50));

        Assert.Contains("PUMBILITY peers", cut.Markup);
        Assert.Contains("[P.B] DIAMOND LV.1 – [P.B] RED BERYL LV.2", cut.Markup);
        Assert.Contains("you stand on [P.B] DIAMOND LV.4", cut.Markup);
        Assert.DoesNotContain("you sit at", cut.Markup);
        Assert.DoesNotContain("Stale weight", cut.Markup);
    }

    [Fact]
    public void OnACompetitiveBandTheStatsKeepTheLevelFigures()
    {
        var cut = RenderPage(peers: PeerGroup.Competitive(18.4, 0.5, 148));

        Assert.Contains("you sit at 18.4", cut.Markup);
        Assert.Contains("Stale weight", cut.Markup);
        Assert.DoesNotContain("PUMBILITY peers", cut.Markup);
    }

    private IRenderedFragment RenderPage(string? savedSort = null, bool movers = false, PeerGroup? peers = null)
    {
        var charts = Folder
            .Select(f => ChartNamed(f.Name))
            .ToArray();

        var settings = Settings();
        settings.Setup(s => s.GetSelectedMix(It.IsAny<CancellationToken>())).ReturnsAsync(MixEnum.Phoenix);
        settings.Setup(s => s.GetSetting("Breakdown__SpreadSort", It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(savedSort);

        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User).Returns(new User(UserId, "Tester", true, null, new Uri("https://piu.test/me.png"), null));

        Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        Mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Folder.Zip(charts)
                .Where(p => p.First.Mine != null)
                .Select(p => new RecordedPhoenixScore(p.Second.Id, p.First.Mine!.Value, null, false,
                    new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero)))
                .ToArray());
        Mediator.Setup(m => m.Send(It.IsAny<GetPersonalizedTierListBreakdownQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonalizedTierListBreakdown(
                Folder.Zip(charts)
                    .Select((p, i) => new BreakdownChartRecord(p.Second.Id,
                        TierListCategory.Medium,
                        // Two charts disagree with the community, and the last has no projection
                        // at all — the shape that made the old predicate print a negative.
                        movers && i < 2 ? TierListCategory.Hard : TierListCategory.Medium,
                        TierListCategory.Unrecorded, TierListCategory.Unrecorded,
                        TierListCategory.Medium,
                        movers && i == 3 ? null : p.First.Projected))
                    .ToArray(),
                Array.Empty<BreakdownSkillRecord>(), false, 0, 0, 0, 0,
                0, 0, 0, 1, Folder.Length, Folder.Length, 148, 18.4, 0.5, 0.7, false, peers));

        this.RenderInteractive();
        // The lens is [SupplyParameterFromQuery], so it arrives through the URL rather than as a
        // parameter — bUnit rejects setting one directly and says so.
        var nav = (NavigationManager)Services.GetService(typeof(NavigationManager))!;
        nav.NavigateTo("/TierLists/Double/18/Breakdown?Lens=Score");
        return Render(builder =>
        {
            builder.OpenComponent<MudPopoverProvider>(0);
            builder.CloseComponent();
            builder.OpenComponent<PersonalizedBreakdown>(1);
            builder.AddAttribute(2, nameof(PersonalizedBreakdown.ChartTypeRoute), "Double");
            builder.AddAttribute(3, nameof(PersonalizedBreakdown.LevelRoute), 18);
            builder.CloseComponent();
        });
    }

    private static Chart ChartNamed(string name)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromSeconds(125), "BanYa", Bpm.From(160, 160)),
            ScoreTracker.SharedKernel.Enums.ChartType.Double, 18, MixEnum.Phoenix, "SUNNY", 700,
            new HashSet<Skill>());
    }
}
