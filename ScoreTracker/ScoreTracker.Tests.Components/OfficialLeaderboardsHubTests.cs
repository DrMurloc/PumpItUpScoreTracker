using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bunit;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Pages.OfficialLeaderboards;
using Xunit;
using ChartType = ScoreTracker.SharedKernel.Enums.ChartType;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The Official Leaderboards hub views (docs/design/official-leaderboards-overhaul.md):
///     each renders from its snapshot-backed query with honest empty states. The
///     sweep-to-snapshot path is integration/E2E territory; these pin the rendering rules.
/// </summary>
public sealed class OfficialLeaderboardsHubTests : ComponentTestBase
{
    private readonly Mock<IMediator> _mediator = new();
    private static readonly DateTimeOffset Week2 = new(2026, 7, 12, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Week1 = Week2.AddDays(-7);

    public OfficialLeaderboardsHubTests()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());
        Services.AddSingleton(_mediator.Object);
        // Last: it reads the renderer, locking the service collection. The hub views run in
        // an interactive circuit; SongImage/DifficultyBubble gate tooltips on RendererInfo.
        this.RenderInteractive();
    }

    private static Chart MakeChart(string name = "District 1", ChartType type = ChartType.Double,
        int level = 26) =>
        new(Guid.NewGuid(), MixEnum.Phoenix2,
            new Song(name, SongType.Arcade, new Uri("https://piu.test/art.png"),
                TimeSpan.FromMinutes(2), "Doin", Bpm.From(195, 195)),
            type, level, MixEnum.Phoenix2, null, 1200, new HashSet<Skill>());

    private static OfficialPlayerRecord Player(int id = 1, string name = "VOLTEDGE", bool linked = false) =>
        new(id, name, null, linked ? Guid.NewGuid() : null);

    // ── This Week ────────────────────────────────────────────────────────────

    [Fact]
    public void ThisWeekShowsTheFirstSnapshotEmptyStateBeforeAnySeal()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WeeklyHighlightsRecord?)null);

        var cut = RenderComponent<HubThisWeek>(p => p.Add(x => x.Mix, MixEnum.Phoenix2));

        Assert.Contains("first weekly snapshot", cut.Markup);
    }

    [Fact]
    public void ThisWeekRendersMoversFirstsAndNumberOnes()
    {
        var chart = MakeChart();
        var record = new WeeklyHighlightsRecord(Week2, Week1,
            new[]
            {
                new OfficialMoverRecord(Player(), 31, 17, 18204.51m),
                new OfficialMoverRecord(Player(2, "PUMPJACK"), 27, 18, 18101.09m)
            },
            new[] { new OfficialBoardsClimbedRecord(Player(3, "HALCYON"), 21, 388) },
            new[]
            {
                new OfficialGradeFirstRecord(Player(), chart.Id, "Double", 26, "PG", 1_000_000, true),
                new OfficialGradeFirstRecord(Player(2, "PUMPJACK"), chart.Id, "Double", 26, "SSS", 991_000,
                    false)
            },
            new[]
            {
                new OfficialNewNumberOneRecord(Player(3, "HALCYON"), chart.Id, 997_342, Player(4, "MIRAGE"))
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(record);

        var cut = RenderComponent<HubThisWeek>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [chart.Id] = chart }));

        Assert.Contains("▲14", cut.Markup); // 31 → 17
        Assert.Contains("VOLTEDGE", cut.Markup);
        Assert.Contains("+21 boards", cut.Markup.Replace("<b>", "").Replace("</b>", ""));
        Assert.Contains("PG", cut.Markup);
        Assert.Contains("First in the folder", cut.Markup);
        Assert.Contains("dethroned MIRAGE", cut.Markup);
    }

    [Fact]
    public void ThisWeekLabelsAGapWiderThanOneWeek()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(Week2, Week2.AddDays(-14),
                new[] { new OfficialMoverRecord(Player(), 5, 3, 17000m) },
                Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(),
                Array.Empty<OfficialNewNumberOneRecord>()));

        var cut = RenderComponent<HubThisWeek>(p => p.Add(x => x.Mix, MixEnum.Phoenix2));

        Assert.Contains("(2 weeks)", cut.Markup);
    }

    // ── Rankings ─────────────────────────────────────────────────────────────

    [Fact]
    public void RankingsShowOfficialCaptionDeltasAndArchetypes()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(Week2, true, new[]
            {
                new OfficialRankingRecord(1, 2, Player(1, "STARFORGE", linked: true), 19412.88m, 241,
                    RecapPlayerType.Perfectionist),
                new OfficialRankingRecord(2, 1, Player(2, "MIRAGE"), 19205.13m, 228,
                    RecapPlayerType.Competitive)
            }));

        var cut = RenderComponent<HubRankings>(p => p.Add(x => x.Mix, MixEnum.Phoenix2));

        Assert.Contains("official PUMBILITY board", cut.Markup);
        Assert.Contains("▲1", cut.Markup);
        Assert.Contains("▼1", cut.Markup);
        Assert.Contains("19,412.88", cut.Markup);
        Assert.Contains("Perfectionist", cut.Markup);
    }

    [Fact]
    public void RankingsShowTheComputedCaptionWhenNoOfficialBoardExists()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(Week2, false, new[]
            {
                new OfficialRankingRecord(1, null, Player(), 123456m, 100, null)
            }));

        var cut = RenderComponent<HubRankings>(p => p.Add(x => x.Mix, MixEnum.Phoenix));

        Assert.Contains("computed rating", cut.Markup);
        Assert.Contains("123,456", cut.Markup);
    }

    [Fact]
    public void RankingsBoardsCountLinksIntoThePlayersView()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(Week2, true, new[]
            {
                new OfficialRankingRecord(1, null, Player(1, "STARFORGE"), 19412.88m, 241,
                    null)
            }));
        var shown = string.Empty;
        var cut = RenderComponent<HubRankings>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.OnShowPlayer, (string username) => shown = username));

        cut.FindAll("a").First(a => a.TextContent.Trim() == "241").Click();

        Assert.Equal("STARFORGE", shown);
    }

    // ── Popularity ───────────────────────────────────────────────────────────

    [Fact]
    public void PopularityRendersTrendArrowsAndNewEntrants()
    {
        var climbing = MakeChart("Papasito", ChartType.Single, 20);
        var fresh = MakeChart("Move Like This", ChartType.Single, 18);
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OfficialPopularityRecord(climbing.Id, 1, 3, new[] { 4, 3, 1 }),
                new OfficialPopularityRecord(fresh.Id, 2, null, new[] { 2 })
            });

        var cut = RenderComponent<HubPopularity>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Charts, new Dictionary<Guid, Chart>
            {
                [climbing.Id] = climbing, [fresh.Id] = fresh
            }));

        Assert.Contains("Papasito", cut.Markup);
        Assert.Contains("▲2", cut.Markup);
        Assert.Contains("olb-spark", cut.Markup);
        Assert.Contains("＊", cut.Markup); // new-this-week marker
    }

    [Fact]
    public void PopularitySongsViewAggregatesByBestChart()
    {
        var single = MakeChart("Papasito", ChartType.Single, 20);
        var doubles = MakeChart("Papasito", ChartType.Double, 21);
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OfficialPopularityRecord(single.Id, 4, 5, new[] { 5, 4 }),
                new OfficialPopularityRecord(doubles.Id, 2, 2, new[] { 2, 2 })
            });
        var cut = RenderComponent<HubPopularity>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Charts, new Dictionary<Guid, Chart>
            {
                [single.Id] = single, [doubles.Id] = doubles
            }));

        cut.FindAll("button").First(b => b.TextContent.Contains("Songs")).Click();

        // One row for the song, ranked by its best chart (the D21 at place 2).
        Assert.Single(cut.FindAll(".olb-poprow"));
    }

    [Fact]
    public void PopularityFolderFilterNarrowsTheBoardAndClearsBack()
    {
        var s20 = MakeChart("Papasito", ChartType.Single, 20);
        var d24 = MakeChart("Witch Doctor", ChartType.Double, 24);
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPopularityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new OfficialPopularityRecord(s20.Id, 1, 1, new[] { 1 }),
                new OfficialPopularityRecord(d24.Id, 2, 2, new[] { 2 })
            });
        var cut = RenderComponent<HubPopularity>(p => p
            .Add(x => x.Mix, MixEnum.Phoenix2)
            .Add(x => x.Charts, new Dictionary<Guid, Chart> { [s20.Id] = s20, [d24.Id] = d24 }));
        Assert.Equal(2, cut.FindAll(".olb-poprow").Count);

        cut.InvokeAsync(() => cut.Instance.SetFolder((ChartType.Single, 20)));

        Assert.Single(cut.FindAll(".olb-poprow"));
        Assert.Contains("Papasito", cut.Markup);
        Assert.DoesNotContain("Witch Doctor", cut.Markup);

        cut.InvokeAsync(() => cut.Instance.ClearFolder());

        Assert.Equal(2, cut.FindAll(".olb-poprow").Count);
    }

    // ── Players ──────────────────────────────────────────────────────────────

    [Fact]
    public void PlayersViewRendersProfileTilesAfterSelection()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerNamesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "NIMBUS9" });
        // A single history point keeps the ApexCharts pair unrendered — chart internals are
        // a JS concern, not this fact's; the tiles are.
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialPlayerProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialPlayerProfileRecord(Player(9, "NIMBUS9"),
                RecapPlayerType.Competitive, 17903.40m, 8, 1, 182, 4, 1, 61,
                new[] { new OfficialPlayerHistoryPoint(Week2, 17903.40m, 8, 182) },
                Array.Empty<OfficialPlayerChartRecord>()));

        var cut = RenderComponent<HubPlayers>(p => p.Add(x => x.Mix, MixEnum.Phoenix2));
        cut.InvokeAsync(() => cut.Instance.SelectPlayer("NIMBUS9"));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("17,903.40", cut.Markup);
            Assert.Contains("182", cut.Markup);
            Assert.Contains("Competitive", cut.Markup);
        });
    }
}
