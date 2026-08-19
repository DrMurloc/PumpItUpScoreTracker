using System.Collections.Generic;
using Bunit;
using ScoreTracker.Domain.Services.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Web.Components;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Phoenix 2 peer line under "What to play next" (docs/design/pumbility-overhaul.md §3.3,
///     D27, D28): a chip with the count for a lit type, "N/50 charts" for a dark one, one chip per
///     type on the All pool, the definition in the tooltip rather than on the line, and nothing at
///     all for a competitive band.
/// </summary>
public sealed class PumbilityPeerLineTests : ComponentTestBase
{
    [Fact]
    public void ALitTypeIsACountWithTheDefinitionInItsTooltip()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(24, 23, 58)
            })
            .Add(x => x.Pool, ChartType.Single));

        var chip = cut.Find(".pmb-peer-chip");
        Assert.Equal("true", chip.GetAttribute("data-lit"));
        Assert.Equal("23 PUMBILITY peers", chip.TextContent.Trim());
        // The line is a count; the sentence lives in the chip's title.
        Assert.DoesNotContain("within 3 levels", chip.TextContent);
        var title = chip.GetAttribute("title")!;
        Assert.Contains("within 3 levels of you with a full pool", title);
        Assert.Contains("Charts fewer than 5 of them have passed are not shown.", title);
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

        var chip = cut.Find(".pmb-peer-chip");
        Assert.Equal("false", chip.GetAttribute("data-lit"));
        Assert.Equal("Doubles: 29/50 charts", chip.TextContent.Trim());
        Assert.Contains("Peer projections show once your doubles pool has 50 charts.", chip.GetAttribute("title")!);
        Assert.DoesNotContain("PUMBILITY peers", chip.TextContent);
    }

    [Fact]
    public void TheAllPoolShowsOneChipPerTypeNamingTheType()
    {
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Pumbility(24, 23, 58),
                [ChartType.Double] = PeerGroup.Pumbility(24, 16, 29)
            })
            .Add(x => x.Pool, (ChartType?)null));

        var chips = cut.FindAll(".pmb-peer-chip");
        Assert.Equal(2, chips.Count);
        Assert.Equal("Singles: 23 PUMBILITY peers", chips[0].TextContent.Trim());
        Assert.Equal("Doubles: 29/50 charts", chips[1].TextContent.Trim());
    }

    [Fact]
    public void ACompetitiveBandPrintsItsSizesAndNamesItselfInTheTooltip()
    {
        // Phoenix 1's peers are the competitive band (D43): the chips count it and the title says
        // what it is, with the same five-peer clause.
        var cut = RenderComponent<PumbilityPeerLine>(p => p
            .Add(x => x.Peers, new Dictionary<ChartType, PeerGroup>
            {
                [ChartType.Single] = PeerGroup.Competitive(21.4, 1.0, 144),
                [ChartType.Double] = PeerGroup.Competitive(21.1, 1.0, 98)
            })
            .Add(x => x.Pool, (ChartType?)null));

        var chips = cut.FindAll(".pmb-peer-chip");
        Assert.Equal(new[] { "Singles: 144 peers", "Doubles: 98 peers" }, chips.Select(c => c.TextContent.Trim()).ToArray());
        Assert.All(chips, c => Assert.Equal("true", c.GetAttribute("data-lit")));
        var title = chips[0].GetAttribute("title")!;
        Assert.Contains("within one competitive level of you", title);
        Assert.DoesNotContain("PUMBILITY", title);
        Assert.Contains("Charts fewer than 5 of them have passed are not shown.", title);
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

        Assert.Single(cut.FindAll(".pmb-peer-chip"));
    }
}
