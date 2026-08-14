using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PersonalizedBreakdownHandlerTests
{

    [Fact]
    public async Task ScoreLensPersonalizesThroughTheProjectionAlone()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Score", Guid.NewGuid()), CancellationToken.None);

        // The projection is the whole recipe: no community share, and neither of the two
        // personal sources it replaced.
        Assert.Equal(1, result.ProjectionWeight);
        Assert.Equal(0, result.CommunityWeight);
        Assert.Equal(0, result.SkillWeight);
        Assert.Equal(0, result.SimilarPlayersWeight);
    }

    [Fact]
    public async Task AProjectionNoPeerCanAnswerReportsSilenceRatherThanAgreement()
    {
        // The default fixture parks every player at the competitive-level-1 no-data floor, so the
        // projector declines. Silence has to be visible AS silence: a personalized column that
        // simply equals the community one is the failure this count exists to expose.
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());
        var handler = BuildHandler(charts: charts, mediator: mediator);

        var result = await handler.Handle(Query("Score", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(0, result.ProjectedChartCount);
        Assert.Equal(1, result.FolderChartCount);
        Assert.Equal(TierListCategory.Unrecorded,
            result.Charts.Single(c => c.ChartId == chart.Id).ProjectionCategory);
    }

    [Fact]
    public async Task TheCohortBehindTheNumbersTravelsWithThem()
    {
        // The page states these instead of describing the cohort in a sentence, so each has to be
        // the figure it claims: players who actually voted, the level they were matched around,
        // the band's half-width, and how discounted their evidence is.
        var easier = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var middling = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var harder = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var userId = Guid.NewGuid();
        var settled = Guid.NewGuid();
        var improving = Guid.NewGuid();

        var charts = ChartsMock(new[] { easier, middling, harder });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());

        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatsFor(userId, doublesCompetitive: 17.5));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Double, 17.5,
                TierListBlendBuilder.ProjectionCompetitiveWindow, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { settled, improving, userId });
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                StatsFor(settled, doublesCompetitive: 17.5), StatsFor(improving, doublesCompetitive: 17.5)
            });

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Double, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                PeerScoreOn(settled, easier, 985_000), PeerScoreOn(improving, easier, 980_000),
                PeerScoreOn(settled, middling, 950_000), PeerScoreOn(improving, middling, 945_000),
                PeerScoreOn(settled, harder, 910_000), PeerScoreOn(improving, harder, 905_000)
            });

        // One peer was a level lower when they set every one of these, so exp(-1) of their voice
        // survives; the other has not moved and keeps all of theirs.
        var history = new Mock<IPlayerHistoryRepository>();
        history.Setup(h => h.GetHistory(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new PlayerRatingRecord(improving, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    16.5, 0, 16.5, 0, 0)
            });

        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scores,
            playerStats: playerStats, history: history);

        var result = await handler.Handle(Query("Score", userId), CancellationToken.None);

        // The requesting player is in the cohort read and must not count as their own peer.
        Assert.Equal(2, result.PeerCount);
        Assert.Equal(17.5, result.CompetitiveLevel);
        Assert.Equal(TierListBlendBuilder.ProjectionCompetitiveWindow, result.CompetitiveWindow);
        // Six scores: three at full voice, three at exp(-1). Averaged per SCORE, not per player —
        // a peer who lent three scores lent three pieces of evidence.
        Assert.Equal((1 + Math.Exp(-1)) / 2, result.MeanFreshness, 6);
    }

    [Fact]
    public async Task NonPersonalizingLensThrows()
    {
        var handler = BuildHandler();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            handler.Handle(Query("Popularity", Guid.NewGuid()), CancellationToken.None));
    }

    private static GetPersonalizedTierListBreakdownQuery Query(string lens, Guid userId)
    {
        return new GetPersonalizedTierListBreakdownQuery(ChartType.Double, DifficultyLevel.From(17), lens,
            userId);
    }

    private static void SetupTierList(Mock<IMediator> mediator, string name,
        IEnumerable<SongTierListEntry> entries)
    {
        mediator.Setup(m => m.Send(It.IsAny<GetTierListWithFallbackQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(Array.Empty<SongTierListEntry>(), false));
        mediator.Setup(m => m.Send(It.Is<GetTierListWithFallbackQuery>(q => (string)q.TierListName == name),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TierListResult(entries.ToArray(), false));
    }

    private static Mock<IChartRepository> ChartsMock(IEnumerable<Chart> charts)
    {
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

    private static UserPhoenixScore PeerScoreOn(Guid userId, Chart chart, int score)
    {
        return new UserPhoenixScore(userId, chart.Id, "Peer", score, null, false, true,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static PersonalizedBreakdownHandler BuildHandler(
        Mock<IChartRepository>? charts = null,
        Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<IPlayerHistoryRepository>? history = null)
    {
        charts ??= ChartsMock(Array.Empty<Chart>());
        mediator ??= new Mock<IMediator>();
        scores ??= new Mock<IScoreReader>();
        if (playerStats == null)
        {
            playerStats = new Mock<IPlayerStatsReader>();
            playerStats.Setup(p => p.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Guid id, CancellationToken _) => StatsFor(id));
        }
        history ??= new Mock<IPlayerHistoryRepository>();
        var tierLists = new Mock<ITierListRepository>();
        tierLists.Setup(c => c.GetPumbilityTierList(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                It.IsAny<DifficultyLevel>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PumbilityTierListFolder(Array.Empty<PumbilityTierListRecord>(), 0));
        return new PersonalizedBreakdownHandler(mediator.Object, charts.Object,
            new Mock<ICurrentUserAccessor>().Object, new MemoryCache(new MemoryCacheOptions()),
            new ScoreProjector(scores.Object, playerStats.Object, history.Object),
            tierLists.Object, new Mock<ITitleRepository>().Object, playerStats.Object, scores.Object);
    }
}
