using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PersonalizedBreakdownHandlerTests
{
    [Fact]
    public async Task ChartsCarryCommunityPersonalizedAndPerSourceCategories()
    {
        // The K7 skill setup from the blend tests: strong Runs, weak Twists. The
        // stored Pass Count list holds both folder charts at Medium, so the
        // personalized column must move the runs chart easier and the twists chart
        // harder while the community column stays Medium — the movers diff.
        var userId = Guid.NewGuid();
        var scored = new List<Chart>();
        var scores = new List<RecordedPhoenixScore>();
        var chips = new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>();
        for (var i = 0; i < 6; i++)
        {
            var runs = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            var twists = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            var jumps = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
            scored.AddRange(new[] { runs, twists, jumps });
            scores.Add(new RecordedPhoenixScore(runs.Id, 960_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            scores.Add(new RecordedPhoenixScore(twists.Id, 880_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            scores.Add(new RecordedPhoenixScore(jumps.Id, 920_000 + i * 1000, null, false, DateTimeOffset.MinValue));
            chips[runs.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) };
            chips[twists.Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 0.8m) };
            chips[jumps.Id] = new[] { new ChartSkillChipRecord(Skill.Jumps, true, 0.8m) };
        }

        // Three folder charts: the middle (jumps) one keeps the estimate spread wide
        // enough that the runs/twists extremes band past the blend's ±0.5 boundary —
        // two symmetric points alone sit at exactly ±1σ and quantize to Medium.
        var runsFolderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var twistsFolderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var jumpsFolderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        chips[runsFolderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) };
        chips[twistsFolderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 0.8m) };
        chips[jumpsFolderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Jumps, true, 0.8m) };

        var charts = ChartsMock(scored.Concat(new[] { runsFolderChart, twistsFolderChart, jumpsFolderChart }));
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count", new[]
        {
            new SongTierListEntry("Pass Count", runsFolderChart.Id, TierListCategory.Medium, 0),
            new SongTierListEntry("Pass Count", twistsFolderChart.Id, TierListCategory.Medium, 0),
            new SongTierListEntry("Pass Count", jumpsFolderChart.Id, TierListCategory.Medium, 0)
        });
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chips);
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scoreReader);

        var result = await handler.Handle(Query("Pass", userId), CancellationToken.None);

        var runsRecord = result.Charts.Single(c => c.ChartId == runsFolderChart.Id);
        var twistsRecord = result.Charts.Single(c => c.ChartId == twistsFolderChart.Id);
        Assert.Equal(TierListCategory.Medium, runsRecord.CommunityCategory);
        Assert.Equal(TierListCategory.Medium, twistsRecord.CommunityCategory);
        Assert.True(runsRecord.PersonalizedCategory < runsRecord.CommunityCategory,
            $"the runs chart should move easier ({runsRecord.PersonalizedCategory})");
        Assert.True(twistsRecord.PersonalizedCategory > twistsRecord.CommunityCategory,
            $"the twists chart should move harder ({twistsRecord.PersonalizedCategory})");
        Assert.NotEqual(TierListCategory.Unrecorded, runsRecord.SkillCategory);
        Assert.True(result.SkillSourceActive);
        Assert.Equal(3, result.UsableSkillCount);
        Assert.Equal(18, result.ScoredChartCount);
        Assert.True(result.Skills.Single(s => s.Skill == Skill.Runs).Deviation > 0);
        Assert.True(result.Skills.Single(s => s.Skill == Skill.Twists).Deviation < 0);
    }

    [Fact]
    public async Task SilentSkillSourceReportsInactiveAndPersonalizedMatchesCommunity()
    {
        // One scored chart can't clear the evidence gate: the breakdown must say the
        // source is inactive AND show the consequence — every chart's personalized
        // tier equals the community tier.
        var userId = Guid.NewGuid();
        var lone = new ChartBuilder().WithLevel(16).WithType(ChartType.Double).Build();
        var folderChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { lone, folderChart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Pass Count",
            new[] { new SongTierListEntry("Pass Count", folderChart.Id, TierListCategory.Hard, 0) });
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>
            {
                [lone.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) },
                [folderChart.Id] = new[] { new ChartSkillChipRecord(Skill.Runs, true, 0.8m) }
            });
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
                { new RecordedPhoenixScore(lone.Id, 950_000, null, false, DateTimeOffset.MinValue) });
        var handler = BuildHandler(charts: charts, mediator: mediator, scores: scoreReader);

        var result = await handler.Handle(Query("Pass", userId), CancellationToken.None);

        Assert.False(result.SkillSourceActive);
        Assert.True(result.UsableSkillCount < 3);
        var record = result.Charts.Single(c => c.ChartId == folderChart.Id);
        Assert.Equal(TierListCategory.Unrecorded, record.SkillCategory);
        Assert.Equal(record.CommunityCategory, record.PersonalizedCategory);
    }

    [Fact]
    public async Task ExposesLensWeightsAndNeighborCount()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var userId = Guid.NewGuid();
        var neighbor = Guid.NewGuid();
        var charts = ChartsMock(new[] { chart });
        var mediator = new Mock<IMediator>();
        SetupTierList(mediator, "Scores", Array.Empty<SongTierListEntry>());
        mediator.Setup(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SongTierListEntry>());
        var playerStats = new Mock<IPlayerStatsReader>();
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(StatsFor(userId, doublesCompetitive: 17.5));
        playerStats.Setup(p => p.GetPlayersByCompetitiveRange(MixEnum.Phoenix, ChartType.Double, 17.5, 1.0,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { neighbor });
        playerStats.Setup(p => p.GetStats(MixEnum.Phoenix, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { StatsFor(neighbor, doublesCompetitive: 17.5) });
        var handler = BuildHandler(charts: charts, mediator: mediator, playerStats: playerStats);

        var result = await handler.Handle(Query("Score", userId), CancellationToken.None);

        // Score lens: Scores x2 + Official Scores x1 fold into the community column.
        Assert.Equal(3, result.CommunityWeight);
        Assert.Equal(2, result.SkillWeight);
        Assert.Equal(1, result.SimilarPlayersWeight);
        Assert.Equal(1, result.SimilarPlayerCount);
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

    private static PersonalizedBreakdownHandler BuildHandler(
        Mock<IChartRepository>? charts = null,
        Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null,
        Mock<IPlayerStatsReader>? playerStats = null,
        Mock<IUserTierListRepository>? userTierLists = null)
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
        userTierLists ??= new Mock<IUserTierListRepository>();
        return new PersonalizedBreakdownHandler(mediator.Object, charts.Object, scores.Object,
            playerStats.Object, userTierLists.Object, new Mock<ICurrentUserAccessor>().Object,
            new MemoryCache(new MemoryCacheOptions()));
    }
}
