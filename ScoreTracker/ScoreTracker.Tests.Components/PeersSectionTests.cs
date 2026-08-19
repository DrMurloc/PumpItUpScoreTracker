using Bunit;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Enums;
using ScoreTracker.Web.Services.Contracts;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Play block (docs/design/pumbility-overhaul.md §3.10): it reads the peers page off the
///     mediator for the frame's pool, opens on Prevalence with the gains switch on, shows the
///     Phoenix 1 switch only under Projected gains and only when carried rows exist, keeps the
///     density trio right above the list, swaps the gains to the peer-only projection when the
///     switch goes off — and on Phoenix 1 is the same block over the competitive band (D43).
/// </summary>
public sealed class PeersSectionTests : ComponentTestBase
{
    private static readonly Guid Viewer = Guid.NewGuid();
    private readonly Dictionary<Guid, Chart> _charts = new();

    public PeersSectionTests()
    {
        Services.AddSingleton(Mock.Of<IUserRepository>());
        CurrentUser.Setup(c => c.IsLoggedIn).Returns(true);
        CurrentUser.Setup(c => c.User).Returns(new User(Viewer, Name.From("Viewer"), true, Name.From("Viewer"),
            new Uri("https://piu.test/a.png"), Name.From("US")));
    }

    [Fact]
    public void OpensOnPrevalenceWithTheGainsSwitchOnAndTheTrioAboveTheList()
    {
        var page = Page(carried: false);
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, page).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        // The controls row: grouped-by, its switch, then the trio at the far end — and nothing in the head.
        Assert.Empty(cut.FindAll(".pmb-block-head .mud-button-group-root"));
        var controls = cut.Find("[data-testid=peers-controls]");
        Assert.NotNull(controls.QuerySelector(".pmb-peers-controls-end .mud-button-group-root"));
        Assert.NotNull(controls.QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.Null(controls.QuerySelector("[data-testid=peers-p1-switch]"));
        // Prevalence + the gains cut: only the paying chart shows, in its tier.
        Assert.Contains("Staple", cut.Markup);
        Assert.Contains("Pays", cut.Markup);
        Assert.DoesNotContain("Free", cut.Markup);
        Mediator.Verify(m => m.Send(It.Is<GetPumbilityPeersPageQuery>(q => q.UserId == Viewer && q.Mix == MixEnum.Phoenix2 && q.Pool == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ThePhoenix1SwitchAppearsOnlyUnderProjectedGainsAndOnlyWithCarriedRows()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.ProjectedGains.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var withCarried = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        withCarried.WaitForState(() => withCarried.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.NotNull(withCarried.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-p1-switch]"));
        Assert.Null(withCarried.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-gains-switch]"));
        // Under Projected gains the carried row interleaves by gain and the section is a band.
        Assert.Contains("+15 to +25", withCarried.Markup);
        Assert.Contains("Further", withCarried.Markup);

        var without = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false)).Add(x => x.Charts, _charts));
        without.WaitForState(() => without.FindAll("[data-testid=peers-controls]").Count > 0);
        Assert.Null(without.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-p1-switch]"));
    }

    [Fact]
    public async Task TurningThePhoenix1SwitchOffReadsThePeerOnlyProjectionAndDropsTheCarriedRows()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var pays = _charts.Values.First(c => c.Song.Name == "Pays");
        Mediator.Setup(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityProjection(
                new Dictionary<Guid, PhoenixScore> { [pays.Id] = 987_475 },
                new Dictionary<Guid, double> { [pays.Id] = 12.5 },
                new Dictionary<Guid, TierListCategory>()));
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.ProjectedGains.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-p1-switch]").Count > 0);
        Assert.Contains("Further", cut.Markup);
        Assert.Contains("+18", cut.Markup);

        await cut.Find("[data-testid=peers-p1-switch]").ChangeAsync(new ChangeEventArgs { Value = false });
        cut.WaitForState(() => !cut.Markup.Contains("Further"));

        Assert.DoesNotContain("Further", cut.Markup);
        Assert.Contains("+12", cut.Markup); // the peer-only gain now prints on the paying chart
        Mediator.Verify(m => m.Send(It.IsAny<ProjectPumbilityGainsQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        settings.Verify(s => s.SetSetting(PeersSection.ProjectPhoenix1SettingKey, "False"), Times.Once);
    }

    [Fact]
    public void TheCompareStripSaysHowYourPoolDiffersPerLitType()
    {
        var page = new PumbilityPeersPageRecord(MixEnum.Phoenix2, null,
            new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = PeerGroup.Pumbility(24, 23, 50) },
            Array.Empty<PeerPoolEntry>(), Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null,
            new Dictionary<ChartType, PeerCompare>
            {
                [ChartType.Single] = new(9, 21, 10,
                    new Dictionary<int, int> { [20] = 16, [21] = 19, [26] = 1 },
                    new Dictionary<int, double> { [20] = .26, [21] = .26, [22] = .27 })
            });

        var cut = RenderComponent<PeerCompareStrip>(p => p.Add(x => x.Page, page));

        Assert.Contains("9 of your 50 singles sit in your peers' top 50.", cut.Markup);
        Assert.Contains("10 singles no peer holds.", cut.Markup);
        Assert.Contains("21 singles held by one at most.", cut.Markup);
        // One axis for both rows, spanning every level either side reaches: 20 through 26.
        Assert.Equal(7, cut.FindAll(".pmb-levelaxis span").Count);
        Assert.Equal(7, cut.FindAll(".pmb-levelrow-you i").Count);
        Assert.Contains("You: 19 charts at 21", cut.Markup);
        Assert.Contains("Peers: 27% of their pool weight at 22", cut.Markup);
    }

    // ------------------------------------------------------------------ fixture

    private Chart NewChart(string name)
    {
        var chart = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            ChartType.Single, 21, MixEnum.Phoenix2, null, null, new HashSet<Skill>());
        _charts[chart.Id] = chart;
        return chart;
    }

    [Fact]
    public void OnPhoenix1TheSameBlockRendersOverTheCompetitiveBand()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers(MixEnum.Phoenix));

        this.RenderInteractive();
        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: false, MixEnum.Phoenix)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        Assert.Equal("Your peers", cut.Find(".pmb-block-title").TextContent.Trim());
        Assert.Contains("within one competitive level of you", cut.Find(".pmb-block-lede").TextContent);
        Assert.DoesNotContain("PUMBILITY levels", cut.Markup);
        // The chips line counts the band; the list and the controls are the Phoenix 2 page's.
        Assert.Equal("Singles: 144 peers", cut.Find(".pmb-peer-chip").TextContent.Trim());
        Assert.NotNull(cut.Find("[data-testid=peers-controls]").QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.Contains("Staple", cut.Markup);
        Mediator.Verify(m => m.Send(It.Is<GetPumbilityPeersPageQuery>(q => q.UserId == Viewer && q.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void YourTop50IsTheThirdOptionAndCarriesNoSwitches()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        var settings = new Mock<IUiSettingsAccessor>();
        settings.Setup(s => s.GetSetting(PeersSection.GroupBySettingKey)).ReturnsAsync(PeerGrouping.YourTop50.ToString());
        Services.AddSingleton(settings.Object);
        this.RenderInteractive();

        var cut = RenderComponent<PeersSection>(p => p.Add(x => x.Page, Page(carried: true)).Add(x => x.Charts, _charts));
        cut.WaitForState(() => cut.FindAll("[data-testid=peers-controls]").Count > 0);

        var controls = cut.Find("[data-testid=peers-controls]");
        Assert.Null(controls.QuerySelector("[data-testid=peers-p1-switch]"));
        Assert.Null(controls.QuerySelector("[data-testid=peers-gains-switch]"));
        Assert.NotNull(controls.QuerySelector(".pmb-peers-controls-end .mud-button-group-root"));
        Assert.Contains("Your pool by place", cut.Find(".pmb-block-lede").TextContent);
        // The frame's pool in this fixture is empty, and the lens says so rather than showing tiers.
        Assert.Contains("Nothing in your pool yet", cut.Find("[data-testid=ppl-empty]").TextContent);
        Assert.DoesNotContain("Staple", cut.Markup);
    }

    private static PeerGroup Group(MixEnum mix) =>
        mix == MixEnum.Phoenix2 ? PeerGroup.Pumbility(24, 23, 50) : PeerGroup.Competitive(21.4, 1.0, 144);

    /// <summary>A page record whose target list holds one paying peer row and, optionally, one carried row.</summary>
    private PumbilityPageRecord Page(bool carried, MixEnum mix = MixEnum.Phoenix2)
    {
        var pays = _charts.Values.FirstOrDefault(c => c.Song.Name == "Pays") ?? NewChart("Pays");
        _ = _charts.Values.FirstOrDefault(c => c.Song.Name == "Free") ?? NewChart("Free");
        var targets = new List<PumbilityTarget> { new(pays.Id, 987_475, 18.4, null, false, null) };
        if (carried)
        {
            var further = _charts.Values.FirstOrDefault(c => c.Song.Name == "Further") ?? NewChart("Further");
            targets.Add(new PumbilityTarget(further.Id, 980_127, 24.4, null, false, null, TargetSource.Phoenix1));
        }

        return new PumbilityPageRecord(mix, null, 17_609.59, 345.94, null, Array.Empty<PoolEntry>(),
            Array.Empty<PoolEntry>(), targets,
            Peers: new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = Group(mix) });
    }

    private PumbilityPeersPageRecord Peers(MixEnum mix = MixEnum.Phoenix2)
    {
        var pays = _charts.Values.FirstOrDefault(c => c.Song.Name == "Pays") ?? NewChart("Pays");
        var free = _charts.Values.FirstOrDefault(c => c.Song.Name == "Free") ?? NewChart("Free");
        return new PumbilityPeersPageRecord(mix, null,
            new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = Group(mix) },
            new[]
            {
                new PeerPoolEntry(pays.Id, ChartType.Single, 12, 23, 500, TierListCategory.Overrated, 0, 12, 987_475, 980_000, 991_000, null, null, null, null, null),
                new PeerPoolEntry(free.Id, ChartType.Single, 10, 23, 450, TierListCategory.Overrated, 1, 10, null, null, null, null, null, null, null, null)
            },
            Array.Empty<PeerAloneEntry>(), Array.Empty<PeerRosterEntry>(), 0, null, new Dictionary<ChartType, PeerCompare>());
    }
}
