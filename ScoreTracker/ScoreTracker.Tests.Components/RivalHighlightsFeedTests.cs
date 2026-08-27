using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Components.Rivals;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The feed rows speak in chips (the feeds' short-form rule): a title stands on its name with
///     no rarity claim, a pumbility ladder span compacts to one label, and the sentence the chip
///     dropped survives on the row's hover title.
/// </summary>
public sealed class RivalHighlightsFeedTests : ComponentTestBase
{
    public RivalHighlightsFeedTests()
    {
        var clock = new Mock<IDateTimeOffsetAccessor>();
        clock.SetupGet(c => c.Now).Returns(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        Services.AddSingleton(clock.Object);
        // DifficultyBubble gates its tooltip on RendererInfo; declare the render world.
        this.RenderInteractive();
    }

    private IRenderedComponent<RivalHighlightsFeed> Render(SignificantWin[] wins,
        IReadOnlyDictionary<Guid, Chart>? charts = null, Action<Chart>? onChartClick = null)
    {
        var record = new PlayerHighlightRecord(Guid.NewGuid(), Guid.NewGuid(), "KYLOREN",
            new Uri("https://example.test/avatar.png"), IsPublic: true, MixEnum.Phoenix2,
            new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.Zero), SessionId: null, wins);
        return RenderComponent<RivalHighlightsFeed>(p =>
        {
            p.Add(x => x.Records, new[] { record })
                .Add(x => x.Charts, charts ?? new Dictionary<Guid, Chart>());
            if (onChartClick != null) p.Add(x => x.OnChartClick, onChartClick);
        });
    }

    private IRenderedComponent<RivalHighlightsFeed> Render(params SignificantWin[] wins) =>
        Render(wins, charts: null);

    private static Chart MakeChart(string name)
    {
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Artist", Bpm.From(140, 140)),
            ChartType.Double, 24, MixEnum.Phoenix2, null, 1200);
    }

    [Fact]
    public void ATitleRowIsTheNameAloneWithNoRarityClaim()
    {
        var cut = Render(new SignificantWin(WinKind.BigTitle, TitleName: "SCROOGE"));

        var row = cut.Find(".dash-ch-why");
        Assert.Equal("🏅 [SCROOGE]", row.TextContent.Trim());
    }

    [Fact]
    public void AStoredRareTitleRowRendersLikeAnyOtherTitle()
    {
        // Pre-change rows still carry the retired kind; they read as plain titles now.
        var cut = Render(new SignificantWin(WinKind.RareTitle, TitleName: "SCROOGE", RarityShare: 0.004));

        Assert.Equal("🏅 [SCROOGE]", cut.Find(".dash-ch-why").TextContent.Trim());
    }

    [Fact]
    public void APumbilityLadderSpanCompactsToOneLabel()
    {
        var cut = Render(new SignificantWin(WinKind.PumbilityTitleSpan,
            TitleName: "[S] ADVANCED LV.9", Detail: "[S] ADVANCED LV.6"));

        Assert.Equal("🏅 [S] ADVANCED LV.6 → 9", cut.Find(".dash-ch-why").TextContent.Trim());
    }

    [Fact]
    public void AScoredChipKeepsItsSentenceOnTheHover()
    {
        var cut = Render(new SignificantWin(WinKind.PeerElite, ChartName: "Bee", Difficulty: "D24",
            RarityShare: 0.04, Rank: 4, Score: 987_654));

        var row = cut.Find(".dash-ch-why");
        Assert.Equal("📊 Top 4%", row.TextContent.Trim());
        Assert.Equal("📊 top 4% of peers", row.GetAttribute("title"));
    }

    [Fact]
    public async Task AChartRowOpensTheDialogWhenThePageWiresIt()
    {
        var chart = MakeChart("Bee");
        Chart? opened = null;
        var cut = Render(
            new[] { new SignificantWin(WinKind.PeerElite, ChartId: chart.Id, RarityShare: 0.04, Rank: 4) },
            new Dictionary<Guid, Chart> { [chart.Id] = chart },
            onChartClick: c => opened = c);

        var row = cut.Find(".dash-ch-chart");
        Assert.Contains("dash-clickable", row.ClassName);
        await row.ClickAsync(new MouseEventArgs());
        Assert.Equal(chart.Id, opened!.Id);
    }

    [Fact]
    public void ChartRowsAreInertWhenNoPageWiresTheClick()
    {
        var chart = MakeChart("Bee");
        var cut = Render(
            new[] { new SignificantWin(WinKind.PeerElite, ChartId: chart.Id, RarityShare: 0.04, Rank: 4) },
            new Dictionary<Guid, Chart> { [chart.Id] = chart });

        Assert.DoesNotContain("dash-clickable", cut.Find(".dash-ch-chart").ClassName);
    }

    [Fact]
    public void ALevelUpRowIsTheRungNameWithThePoolOnTheHover()
    {
        // Index 24 = DIAMOND LV.4 (docs/design/pumbility-levels.md).
        var cut = Render(new SignificantWin(WinKind.PumbilityLevelUp, Rank: 24, PoolValue: 17_641));

        var row = cut.Find(".dash-ch-why");
        Assert.Equal("🆙 DIAMOND LV.4", row.TextContent.Trim());
        Assert.Equal("🆙 Reached DIAMOND LV.4 · 17,641", row.GetAttribute("title"));
    }
}
