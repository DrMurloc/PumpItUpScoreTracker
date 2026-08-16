using System.Collections.Generic;
using Bunit;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Phoenix 2 peer line under "What to play next" (docs/design/pumbility-overhaul.md §3.3,
///     D27, D28): the count and definition for a lit type, the "N of 50" for a dark one, one line
///     per type on the All pool, and nothing at all for a competitive band.
/// </summary>
public sealed class PumbilityPeerLineTests : ComponentTestBase
{
    [Fact]
    public void ALitTypePrintsTheCountItsDefinitionAndTheFivePeerClause()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(24, 23, 58)
            })
            .Add(x => x.Pool, ChartType.Single));

        var line = cut.Find(".pmb-peer-line");
        Assert.Equal("true", line.GetAttribute("data-lit"));
        Assert.Contains("23 PUMBILITY peers", cut.Markup);
        Assert.Contains("within 3 levels of you with a full singles pool", cut.Markup);
        Assert.Contains("Charts fewer than 5 of them have passed are not shown.", cut.Markup);
    }

    [Fact]
    public void ADarkTypeSaysHowFarThePoolIsFromLightingUp()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Double] = PeerGroup.Pumbility(24, 16, 29)
            })
            .Add(x => x.Pool, ChartType.Double));

        var line = cut.Find(".pmb-peer-line");
        Assert.Equal("false", line.GetAttribute("data-lit"));
        Assert.Contains("Doubles: 29 of 50 charts before peer projections show", cut.Markup);
        Assert.DoesNotContain("PUMBILITY peers", cut.Markup);
    }

    [Fact]
    public void TheAllPoolShowsOneLinePerType()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(24, 23, 58),
                [ChartType.Double] = PeerGroup.Pumbility(24, 16, 29)
            })
            .Add(x => x.Pool, (ChartType?)null));

        var lines = cut.FindAll(".pmb-peer-line");
        Assert.Equal(2, lines.Count);
        Assert.Contains("23 PUMBILITY peers", cut.Markup);
        Assert.Contains("Doubles: 29 of 50", cut.Markup);
    }

    [Fact]
    public void ACompetitiveBandRendersNothing()
    {
        // Phoenix 1's list carries no peer line: the group is a competitive band, not PUMBILITY peers.
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
                [ChartType.Single] = PeerGroup.Pumbility(24, 23, 58)
            })
            .Add(x => x.Pool, (ChartType?)null));

        Assert.Single(cut.FindAll(".pmb-peer-line"));
    }
}
