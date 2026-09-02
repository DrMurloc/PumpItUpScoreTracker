using System.Collections.Generic;
using Bunit;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The peer line above the Play list (docs/design/pumbility-overhaul.md §3.3, D28): the dark
///     state and only the dark state — "N/50 charts" for a Phoenix 2 type whose pool is not yet
///     full, with the gate in the tooltip. A lit type says nothing here: its count is what the
///     roster below the list says (owner, field test round two), and a competitive band is always
///     lit, so Phoenix 1 never renders a line at all.
/// </summary>
public sealed class PumbilityPeerLineTests : ComponentTestBase
{
    [Fact]
    public void ALitTypePrintsNothingBecauseTheRosterSaysIt()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(17_609.59, 23, 58)
            })
            .Add(x => x.Pool, ChartType.Single));

        Assert.Empty(cut.FindAll(".pmb-peer-line"));
    }

    [Fact]
    public void ADarkTypeSaysHowFarThePoolIsFromLightingUp()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Double] = PeerGroup.Pumbility(17_609.59, 16, 29)
            })
            .Add(x => x.Pool, ChartType.Double));

        var chip = cut.Find(".pmb-peer-chip");
        Assert.Equal("false", chip.GetAttribute("data-lit"));
        Assert.Equal("Doubles: 29/50 charts", chip.TextContent.Trim());
        Assert.Contains("Peer projections show once your doubles pool has 50 charts.", chip.GetAttribute("title")!);
        Assert.DoesNotContain("PUMBILITY peers", chip.TextContent);
    }

    [Fact]
    public void TheAllPoolNamesEachDarkTypeAndSkipsTheLitOne()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(17_609.59, 0, 12),
                [ChartType.Double] = PeerGroup.Pumbility(17_609.59, 16, 29)
            })
            .Add(x => x.Pool, (ChartType?)null));

        var chips = cut.FindAll(".pmb-peer-chip");
        Assert.Equal(2, chips.Count);
        Assert.Equal("Singles: 12/50 charts", chips[0].TextContent.Trim());
        Assert.Equal("Doubles: 29/50 charts", chips[1].TextContent.Trim());
    }

    [Fact]
    public void ACompetitiveBandRendersNothing()
    {
        // Phoenix 1's peers are the competitive band, which is lit at any pool size (D43) — so
        // there is no dark state to explain and no line.
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Competitive(21.4, 1.0, 144),
                [ChartType.Double] = PeerGroup.Competitive(21.1, 1.0, 98)
            })
            .Add(x => x.Pool, (ChartType?)null));

        Assert.Empty(cut.FindAll(".pmb-peer-line"));
    }

    [Fact]
    public void ATypeWithNoGroupIsSkippedRatherThanInvented()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(17_609.59, 0, 12)
            })
            .Add(x => x.Pool, (ChartType?)null));

        Assert.Single(cut.FindAll(".pmb-peer-chip"));
    }
}
