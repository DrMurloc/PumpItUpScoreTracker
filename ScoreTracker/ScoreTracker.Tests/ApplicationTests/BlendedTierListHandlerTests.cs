using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.Domain.Models;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BlendedTierListHandlerTests
{
    [Fact]
    public async Task PopularityLensMapsCategoriesAndLeavesUnrankedChartsUnrecorded()
    {
        var ranked = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var unranked = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { ranked, unranked });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Popularity",
            new[] { new SongTierListEntry("Popularity", ranked.Id, TierListCategory.Easy, 0) });
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Popularity"), CancellationToken.None);

        Assert.Equal(TierListCategory.Easy, result.Entries.Single(e => e.ChartId == ranked.Id).Category);
        Assert.Equal(TierListCategory.Unrecorded, result.Entries.Single(e => e.ChartId == unranked.Id).Category);
        Assert.False(result.IsProvisionalFallback);
    }

    [Fact]
    public async Task ProvisionalFallbackFromAnySourcePropagates()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Popularity",
            new[] { new SongTierListEntry("Popularity", chart.Id, TierListCategory.Easy, 0) },
            isProvisional: true);
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Popularity"), CancellationToken.None);

        Assert.True(result.IsProvisionalFallback);
    }

    [Fact]
    public async Task CommunityPassLensReadsNoPersonalData()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count",
            new[] { new SongTierListEntry("Pass Count", chart.Id, TierListCategory.Hard, 0) });
        var scores = new Mock<IScoreReader>();
        var playerStats = new Mock<IPlayerStatsReader>();
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats);

        var result = await handler.Handle(Query("Pass", personalized: false), CancellationToken.None);

        Assert.Equal(TierListCategory.Hard, result.Entries.Single(e => e.ChartId == chart.Id).Category);
        scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        playerStats.Verify(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnknownLensThrows()
    {
        var handler = BuildHandler();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(Query("Wombo Combo"), CancellationToken.None));
    }

    [Fact]
    public async Task ScoreLensRanksTheFolderByWhatPeersAtYourLevelActuallyScore()
    {
        var comfortable = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var ordinary = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var punishing = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var userId = Guid.NewGuid();
        var peer = Guid.NewGuid();
        var otherPeer = Guid.NewGuid();

        var charts = ChartsMock(new[] { comfortable, ordinary, punishing });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());

        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatsFor(userId, doublesCompetitive: 17.5));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Double, 17.5,
                TierListBlendBuilder.ProjectionCompetitiveWindow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { peer, otherPeer });
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                StatsFor(peer, doublesCompetitive: 17.5), StatsFor(otherPeer, doublesCompetitive: 17.5)
            });

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                PeerScoreOn(peer, comfortable, 985_000), PeerScoreOn(otherPeer, comfortable, 980_000),
                PeerScoreOn(peer, ordinary, 950_000), PeerScoreOn(otherPeer, ordinary, 945_000),
                PeerScoreOn(peer, punishing, 910_000), PeerScoreOn(otherPeer, punishing, 905_000)
            });

        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats);

        var result = await handler.Handle(Query("Score", personalized: true, userId: userId),
            CancellationToken.None);

        var comfortableEntry = result.Entries.Single(e => e.ChartId == comfortable.Id);
        var punishingEntry = result.Entries.Single(e => e.ChartId == punishing.Id);
        Assert.True(comfortableEntry.Category < punishingEntry.Category,
            $"the chart peers score 985k on ({comfortableEntry.Category}) should rank easier " +
            $"than the one they score 910k on ({punishingEntry.Category})");
    }

    [Fact]
    public async Task TooFewProjectedChartsToHaveASpreadStaySilent()
    {
        // One projection has no spread, so it sits at its own mean and would come out stamped
        // the easiest chart in the folder off a single peer's single score. Below the floor the
        // source says nothing — and since it is the whole recipe on Score, saying nothing means
        // an empty list rather than a quiet fallback to the community's.
        var reached = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var unreached = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var userId = Guid.NewGuid();
        var peer = Guid.NewGuid();

        var charts = ChartsMock(new[] { reached, unreached });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", new[]
        {
            new SongTierListEntry("Scores", reached.Id, TierListCategory.Hard, 0),
            new SongTierListEntry("Scores", unreached.Id, TierListCategory.Hard, 1)
        });

        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatsFor(userId, doublesCompetitive: 17.5));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Double, 17.5,
                TierListBlendBuilder.ProjectionCompetitiveWindow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { peer });
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { StatsFor(peer, doublesCompetitive: 17.5) });

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { PeerScoreOn(peer, reached, 985_000) });

        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats);

        var result = await handler.Handle(Query("Score", personalized: true, userId: userId),
            CancellationToken.None);

        // Nothing is rated: the one peer-played chart did not become the folder's easiest on the
        // strength of being the only one anybody had played, and the community's Hard does not
        // stand in for it either — the page shows its "nobody near your level" state instead.
        Assert.All(result.Entries, e => Assert.Equal(TierListCategory.Unrecorded, e.Category));
    }

    [Fact]
    public async Task Phoenix2PumbilityLensReadsTheViewersRungBand()
    {
        // PUMBILITY peers on Phoenix 2 (docs/design/pumbility-tier-list.md §5): a viewer whose
        // total pool is 17,500 stands on DIAMOND LV.3 — badge index 23 — so the lens reads the
        // list keyed on that rung, and only once the viewer holds a full pool of the type.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var pool = FullPool(ChartType.Double);
        var charts = ChartsMock(pool.Append(chart));
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count", Array.Empty<SongTierListEntry>());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pool.Select(c => PeerScoreOn(userId, c, 950_000)).ToArray());
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 17_500, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1));
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(c => c.GetPumbilityTierList(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityTierListFolder(Array.Empty<PumbilityTierListRecord>(), 0));
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats, tierLists: tierLists);

        await handler.Handle(new GetBlendedTierListQuery(ChartType.Double, DifficultyLevel.From(17), "PUMBILITY",
            true, userId, MixEnum.Phoenix2), CancellationToken.None);

        tierLists.Verify(c => c.GetPumbilityTierList(MixEnum.Phoenix2, ChartType.Double, DifficultyLevel.From(17),
            "R23", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheViewerIsNeverOneOfTheirOwnPeers()
    {
        // The stored list is one per peer group and counts every member's pool — the viewer's among
        // them. What the viewer reads has their own taken back out: one from the peer count, one
        // from every chart their pool holds, and nothing from a chart it does not.
        var userId = Guid.NewGuid();
        var mine = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var theirs = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var pool = FullPool(ChartType.Double, 49).Append(mine).ToArray();
        var charts = ChartsMock(pool.Append(theirs));
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count", Array.Empty<SongTierListEntry>());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pool.Select(c => PeerScoreOn(userId, c, 950_000)).ToArray());
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 17_500, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1));
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(c => c.GetPumbilityTierList(MixEnum.Phoenix2, ChartType.Double, DifficultyLevel.From(17),
                "R23", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityTierListFolder(new[]
            {
                new PumbilityTierListRecord(mine.Id, 5, TierListCategory.Medium, 1),
                new PumbilityTierListRecord(theirs.Id, 3, TierListCategory.Hard, 2)
            }, 24));
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats, tierLists: tierLists);

        var result = await handler.Handle(new GetBlendedTierListQuery(ChartType.Double, DifficultyLevel.From(17),
            "PUMBILITY", true, userId, MixEnum.Phoenix2), CancellationToken.None);

        Assert.Equal(23, result.PeerCount);
        Assert.Equal(4, result.Appearances![mine.Id]);
        Assert.Equal(3, result.Appearances[theirs.Id]);
    }

    /// <summary>Priced doubles (or singles) at level 15 — fifty of them is a full pool of the type.</summary>
    private static Chart[] FullPool(ChartType type, int count = 50)
    {
        return Enumerable.Range(0, count)
            .Select(_ => new ChartBuilder().WithLevel(15).WithType(type).Build())
            .ToArray();
    }

    [Fact]
    public async Task Phoenix2PumbilityLensIsSilentWithoutAFullPoolOfTheType()
    {
        // Forty-nine doubles is not a doubles pool (D28): the viewer has no peers for the type and
        // the lens reads no personalized list, whatever their total pool says.
        var userId = Guid.NewGuid();
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count", Array.Empty<SongTierListEntry>());
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Range(0, 49).Select(_ => PeerScoreOn(userId,
                new ChartBuilder().WithLevel(15).WithType(ChartType.Double).Build(), 950_000)).ToArray());
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix2, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, 18_500, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1));
        var tierLists = new Mock<ITierListRepository>();
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats, tierLists: tierLists);

        await handler.Handle(new GetBlendedTierListQuery(ChartType.Double, DifficultyLevel.From(17), "PUMBILITY",
            true, userId, MixEnum.Phoenix2), CancellationToken.None);

        tierLists.Verify(c => c.GetPumbilityTierList(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
            It.IsAny<DifficultyLevel>(), It.Is<string>(k => k != "*"), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserPhoenixScore PeerScoreOn(Guid userId, Chart chart, int score)
    {
        return new UserPhoenixScore(userId, chart.Id, "Peer", score, null, false, true,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static GetBlendedTierListQuery Query(string lens, bool personalized = false, Guid? userId = null)
    {
        return new GetBlendedTierListQuery(ChartType.Double, DifficultyLevel.From(17), lens, personalized, userId);
    }

    private static void SetupTierList(Mock<IMediator> mediator, string name,
        IEnumerable<SongTierListEntry> entries, bool isProvisional = false)
    {
        mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        mediator.Setup(m => m.Send(It.Is<GetTierListWithFallbackQuery>(q => (string)q.TierListName == name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(entries.ToArray(), isProvisional));
    }

    private static Mock<IChartRepository> ChartsMock(IEnumerable<Chart> charts)
    {
        // Honors the level/type filters like the real repository — the handler asks
        // for the folder (level + type) AND the whole mix (the K7 ±3 window).
        var all = charts.ToArray();
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? level, ChartType? type, IEnumerable<Guid>? _,
                    CancellationToken _) =>
                all.Where(c => level == null || c.Level == level).Where(c => type == null || c.Type == type));
        return m;
    }

    private static PlayerStatsRecord StatsFor(Guid userId, double singlesCompetitive = 1,
        double doublesCompetitive = 1)
    {
        return new PlayerStatsRecord(userId,
            TotalRating: 0, HighestLevel: 1, ClearCount: 0, CoOpRating: 0, CoOpScore: 0,
            SkillRating: 0, SkillScore: 0, SkillLevel: 0,
            SinglesRating: 0, SinglesScore: 0, SinglesLevel: 0,
            DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
            CompetitiveLevel: (singlesCompetitive + doublesCompetitive) / 2,
            SinglesCompetitiveLevel: singlesCompetitive,
            DoublesCompetitiveLevel: doublesCompetitive);
    }

    private static BlendedTierListHandler BuildHandler(
        Mock<IChartRepository>? charts = null,
        Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<ITierListRepository>? tierLists = null)
    {
        charts ??= ChartsMock(Array.Empty<Chart>());
        mediator ??= new Mock<IMediator>();
        scores ??= new Mock<IScoreReader>();
        if (playerStats == null)
        {
            // Default: every player sits at the competitive-level-1 no-data floor.
            playerStats = new Mock<IPlayerStatsReader>();
            playerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Guid id, CancellationToken _) => StatsFor(id));
        }
        if (tierLists == null)
        {
            tierLists = new Mock<ITierListRepository>();
            tierLists.Setup(c => c.GetPumbilityTierList(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<DifficultyLevel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PumbilityTierListFolder(Array.Empty<PumbilityTierListRecord>(), 0));
        }
        // The real projector over the same stubbed ports: the Projection source is driven by
        // peer membership and peer scores, which is what these fixtures already set up.
        return new BlendedTierListHandler(mediator.Object, charts.Object,
            new Mock<ICurrentUserAccessor>().Object, new MemoryCache(new MemoryCacheOptions()),
            new ScoreProjector(scores.Object, playerStats.Object, new Mock<IPlayerHistoryRepository>().Object),
            tierLists.Object, new Mock<ITitleRepository>().Object, playerStats.Object, scores.Object);
    }
}
