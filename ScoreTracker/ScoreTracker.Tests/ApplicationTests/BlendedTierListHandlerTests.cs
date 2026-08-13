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
        Mock<IPlayerStatsReader>? playerStats = null)
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
        var census = new Mock<IPumbilityCensusRepository>();
        census.Setup(c => c.GetFolder(It.IsAny<MixEnum>(), It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityCensusFolder(Array.Empty<PumbilityCensusRecord>(), 0));
        // The real projector over the same stubbed ports: the Projection source is driven by
        // cohort membership and peer scores, which is what these fixtures already set up.
        return new BlendedTierListHandler(mediator.Object, charts.Object,
            new Mock<ICurrentUserAccessor>().Object, new MemoryCache(new MemoryCacheOptions()),
            new ScoreProjector(scores.Object, playerStats.Object, new Mock<IPlayerHistoryRepository>().Object),
            census.Object, new Mock<ITitleRepository>().Object, playerStats.Object);
    }
}
