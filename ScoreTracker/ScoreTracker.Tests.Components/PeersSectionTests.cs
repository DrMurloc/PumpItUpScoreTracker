using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Play page's block across a re-read (docs/design/pumbility-overhaul.md D56): the first
///     paint shows the patience card; a change of energy keeps the controls in place and disabled
///     and the previous list pulsing at its own size until the new record lands, so nothing above
///     the list moves and the page never jumps twice.
/// </summary>
public sealed class PeersSectionTests : ComponentTestBase
{
    private static readonly Guid Me = Guid.NewGuid();

    private readonly Chart _chart = new(Guid.NewGuid(), MixEnum.Phoenix2,
        new Song("Song", SongType.Arcade, new Uri("https://piu.test/i.png"), TimeSpan.FromMinutes(2), "Artist", 180),
        ChartType.Single, 21, MixEnum.Phoenix2, null, null);

    public PeersSectionTests()
    {
        CurrentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        CurrentUser.SetupGet(c => c.User)
            .Returns(new User(Me, "Me", true, null, new Uri("https://piu.test/me.png"), null));
        Services.AddSingleton(Mock.Of<IDateTimeOffsetAccessor>(c =>
            c.Now == new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        Services.AddScoped<CommunityGlowReader>();
        // The first paint is a PatienceCard, which draws its phrase through the RNG seam.
        Services.AddSingleton(new Mock<IRandomNumberGenerator>().Object);
        Mediator.Setup(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartIdentityRecord>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyRivalsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalSubject>());
        Mediator.Setup(m => m.Send(It.IsAny<GetMyCommunitiesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        // The cards gate their tooltips on RendererInfo; declare the render world so bUnit can supply it.
        this.RenderInteractive();
    }

    [Fact]
    public void TheFirstPaintShowsThePatienceCardAndNoControls()
    {
        // Nothing is on screen yet to hold a shape, so the card stands in for the whole block.
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(new TaskCompletionSource<PumbilityPeersPageRecord>().Task);

        var cut = Render(Energy.Great);

        Assert.NotEmpty(cut.FindAll(".patience-card"));
        Assert.Empty(cut.FindAll("[data-testid=peers-controls]"));
    }

    [Fact]
    public void AnEnergyChangeKeepsTheControlsAndPulsesThePreviousListInPlace()
    {
        var second = new TaskCompletionSource<PumbilityPeersPageRecord>();
        var reads = 0;
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++reads == 1 ? Task.FromResult(Peers()) : second.Task);

        var cut = Render(Energy.Great);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=peers-controls]")));
        Assert.NotEmpty(cut.FindAll(".tier-chart-card"));
        Assert.Equal("false", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading"));
        Assert.Empty(cut.FindAll("[data-testid=peers-controls] [disabled]"));

        // The frame hands the new energy down with its re-priced record while the peers read for
        // it is still in flight.
        cut.SetParametersAndRender(p => p.Add(x => x.Energy, Energy.TopOfMyGame).Add(x => x.Page, Page(bar: 320, energy: Energy.TopOfMyGame)));

        cut.WaitForAssertion(() =>
        {
            // No card: the controls stay where the reader left them, disabled, and the previous
            // list is still on screen under the pulse.
            Assert.Empty(cut.FindAll(".patience-card"));
            Assert.Equal("true", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading"));
            Assert.NotEmpty(cut.FindAll("[data-testid=peers-controls] [disabled]"));
            Assert.Equal("true", cut.Find("[data-testid=peers-list]").GetAttribute("aria-busy"));
            Assert.NotEmpty(cut.FindAll(".pmb-list-reloading .tier-chart-card"));
        });
        Assert.Equal(2, reads);

        second.SetResult(Peers());

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading"));
            Assert.Empty(cut.FindAll("[data-testid=peers-controls] [disabled]"));
            Assert.Empty(cut.FindAll(".pmb-list-reloading"));
            Assert.NotEmpty(cut.FindAll(".tier-chart-card"));
        });
    }

    [Fact]
    public void TheChartIdentityChipsAreReadOncePerPageNotPerEnergy()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Peers());
        // One chart with one chip, so the read has something to keep and the count is honest.
        Mediator.Setup(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, ChartIdentityRecord>
            {
                [_chart.Id] = new(_chart.Id, Array.Empty<IdentityChipRecord>())
            });

        var cut = Render(Energy.Great);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=peers-controls]")));
        cut.SetParametersAndRender(p => p.Add(x => x.Energy, Energy.Good).Add(x => x.Page, Page(energy: Energy.Good)));
        cut.WaitForAssertion(() =>
            Assert.Equal("false", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading")));

        Mediator.Verify(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        Mediator.Verify(m => m.Send(It.IsAny<GetChartIdentityQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void APoolChangeWhosePeersLandBeforeTheFramesRecordStillFlipsToThatRecord()
    {
        // The frame re-renders twice on a pool change: once with the new pool beside the record
        // it still holds, once more when the record for that pool lands. The peers read is the
        // lighter of the two and lands in between. The block has to hold until the record
        // arrives and then render the new peers against it — never the new peers against the
        // old record, and never the old record for good.
        var doubles = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song("DoublesSong", SongType.Arcade, new Uri("https://piu.test/d.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            ChartType.Double, 22, MixEnum.Phoenix2, null, null);
        var reads = 0;
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => Task.FromResult(++reads == 1 ? Peers() : DoublesPeers(doubles)));

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page())
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [_chart.Id] = _chart, [doubles.Id] = doubles })
            .Add(x => x.Energy, Energy.Great));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=peers-controls]")));
        Assert.Equal("Song", cut.Find(".tier-chart-card-name").TextContent);

        // The frame's first render after the click: the new pool, the old record. The peers read
        // for Doubles runs off it and lands at once.
        cut.SetParametersAndRender(p => p.Add(x => x.Pool, ChartType.Double));
        cut.WaitForAssertion(() => Assert.Equal(2, reads));

        // The frame's second render: the record priced for the pool.
        cut.SetParametersAndRender(p => p.Add(x => x.Page, DoublesPage(doubles)).Add(x => x.Pool, ChartType.Double));

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("false", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading"));
            var names = cut.FindAll(".tier-chart-card-name");
            Assert.Single(names);
            Assert.Equal("DoublesSong", names[0].TextContent);
        });
    }

    /// <summary>The frame's record for the Doubles pool: its own lit group and one target that pays.</summary>
    private static PumbilityPageRecord DoublesPage(Chart doubles)
    {
        return new PumbilityPageRecord(MixEnum.Phoenix2, ChartType.Double, 9_000, 250, null, Array.Empty<PoolEntry>(),
            Array.Empty<PoolEntry>(),
            new[] { new PumbilityTarget(doubles.Id, PhoenixScore.From(970_000), 9.0, null, false, null) },
            Peers: new Dictionary<ChartType, PeerGroup> { [ChartType.Double] = PeerGroup.Pumbility(9_000, 20, 50) });
    }

    /// <summary>The peers' record for the Doubles pool: the one chart every peer holds.</summary>
    private static PumbilityPeersPageRecord DoublesPeers(Chart doubles)
    {
        return PumbilityPeersPageRecord.Empty(MixEnum.Phoenix2, ChartType.Double) with
        {
            Peers = new Dictionary<ChartType, PeerGroup> { [ChartType.Double] = PeerGroup.Pumbility(9_000, 20, 50) },
            Entries = new[]
            {
                new PeerPoolEntry(doubles.Id, ChartType.Double, 12, 20, 400, TierListCategory.Overrated, 0, 12,
                    null, null, null, null, PhoenixScore.From(970_000))
            }
        };
    }

    [Fact]
    public void AQuickerSecondChangeSupersedesTheFirstReadsPeers()
    {
        // Singles, then Doubles before the Singles peers land. The Doubles record and peers flip
        // the block; the Singles peers arriving afterwards must not replace them.
        var doubles = new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song("DoublesSong", SongType.Arcade, new Uri("https://piu.test/d.png"), TimeSpan.FromMinutes(2), "Artist", 180),
            ChartType.Double, 22, MixEnum.Phoenix2, null, null);
        var singlesRead = new TaskCompletionSource<PumbilityPeersPageRecord>();
        var doublesRead = new TaskCompletionSource<PumbilityPeersPageRecord>();
        var reads = 0;
        Mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersPageQuery>(), It.IsAny<CancellationToken>()))
            .Returns(() => ++reads switch { 1 => Task.FromResult(Peers()), 2 => singlesRead.Task, _ => doublesRead.Task });

        var cut = RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page())
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [_chart.Id] = _chart, [doubles.Id] = doubles })
            .Add(x => x.Energy, Energy.Great));
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("[data-testid=peers-controls]")));

        cut.SetParametersAndRender(p => p.Add(x => x.Pool, ChartType.Single));
        cut.SetParametersAndRender(p => p.Add(x => x.Pool, ChartType.Double));
        cut.WaitForAssertion(() => Assert.Equal(3, reads));
        cut.SetParametersAndRender(p => p.Add(x => x.Page, DoublesPage(doubles)).Add(x => x.Pool, ChartType.Double));

        doublesRead.SetResult(DoublesPeers(doubles));
        cut.WaitForAssertion(() => Assert.Equal("DoublesSong", cut.Find(".tier-chart-card-name").TextContent));

        var rendered = cut.RenderCount;
        singlesRead.SetResult(Peers());
        cut.WaitForState(() => cut.RenderCount > rendered);
        Assert.Equal("false", cut.Find("[data-testid=peers-controls]").GetAttribute("data-reloading"));
        var names = cut.FindAll(".tier-chart-card-name");
        Assert.Single(names);
        Assert.Equal("DoublesSong", names[0].TextContent);
    }

    private IRenderedComponent<PeersSection> Render(Energy energy)
    {
        return RenderComponent<PeersSection>(p => p
            .Add(x => x.Page, Page())
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [_chart.Id] = _chart })
            .Add(x => x.Energy, energy));
    }

    /// <summary>The frame's record: a lit singles group and one target that pays, so a card renders.</summary>
    private PumbilityPageRecord Page(double bar = 300, Energy energy = Energy.Great)
    {
        return new PumbilityPageRecord(MixEnum.Phoenix2, null, 17_600, bar, null, Array.Empty<PoolEntry>(),
            Array.Empty<PoolEntry>(),
            new[] { new PumbilityTarget(_chart.Id, PhoenixScore.From(980_000), 12.3, null, false, null) },
            Peers: new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = PeerGroup.Pumbility(17_600, 20, 50) },
            Energy: energy);
    }

    /// <summary>The peers' record: the same lit group and the one chart every peer holds.</summary>
    private PumbilityPeersPageRecord Peers()
    {
        return PumbilityPeersPageRecord.Empty(MixEnum.Phoenix2, null) with
        {
            Peers = new Dictionary<ChartType, PeerGroup> { [ChartType.Single] = PeerGroup.Pumbility(17_600, 20, 50) },
            Entries = new[]
            {
                new PeerPoolEntry(_chart.Id, ChartType.Single, 12, 20, 400, TierListCategory.Overrated, 0, 12,
                    null, null, null, null, PhoenixScore.From(980_000))
            }
        };
    }
}
