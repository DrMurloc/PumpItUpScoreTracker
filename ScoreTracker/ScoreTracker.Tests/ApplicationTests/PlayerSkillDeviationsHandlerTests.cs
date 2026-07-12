using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     Pins the published skill-deviation numbers to the tier-list Skill source's
///     semantics (TierListBlendBuilder): same floored proficiency scale, same
///     folder-baseline normalization, same evidence thresholds. The fixture is small
///     enough to hand-compute — if these numbers move, the shared machinery moved,
///     and that is breaking-change review for both consumers.
/// </summary>
public sealed class PlayerSkillDeviationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);

    // Six S18 charts, all scored recently (no age outliers), single-skill chips at
    // full segment coverage. Folder baseline = mean proficiency = 0.5:
    //   980k→.8  960k→.6 (Twists: +.3, +.1 → mean +0.2 → +20,000 score units)
    //   940k→.4  920k→.2 (Brackets: −.1, −.3 → mean −0.2 → −20,000)
    //   970k→.7  930k→.3 (Stamina: +.2, −.2 → mean 0 → 0)
    private static (PlayerSkillDeviationsHandler Handler, Guid UserId) BuildFixture(
        bool includeStaminaChips = true)
    {
        var userId = Guid.NewGuid();
        var charts = Enumerable.Range(0, 6)
            .Select(_ => new ChartBuilder().WithLevel(18).WithType(ChartType.Single).Build())
            .ToArray();
        var scores = new[] { 980_000, 960_000, 940_000, 920_000, 970_000, 930_000 };

        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);

        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts.Select((c, i) =>
                new RecordedPhoenixScore(c.Id, scores[i], PhoenixPlate.FairGame, false, Now.AddDays(-5))));

        var chips = new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>
        {
            [charts[0].Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 1.0m) },
            [charts[1].Id] = new[] { new ChartSkillChipRecord(Skill.Twists, true, 1.0m) },
            [charts[2].Id] = new[] { new ChartSkillChipRecord(Skill.Brackets, true, 1.0m) },
            [charts[3].Id] = new[] { new ChartSkillChipRecord(Skill.Brackets, true, 1.0m) }
        };
        if (includeStaminaChips)
        {
            chips[charts[4].Id] = new[] { new ChartSkillChipRecord(Skill.Stamina, true, 1.0m) };
            chips[charts[5].Id] = new[] { new ChartSkillChipRecord(Skill.Stamina, true, 1.0m) };
        }

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(chips);

        var handler = new PlayerSkillDeviationsHandler(mediator.Object, chartRepo.Object, scoreReader.Object,
            Mock.Of<IPlayerStatsReader>(), Mock.Of<IUserTierListRepository>(), FakeDateTime.At(Now).Object);
        return (handler, userId);
    }

    [Fact]
    public async Task DeviationsMatchTheSkillSourceMathInScoreUnits()
    {
        var (handler, userId) = BuildFixture();

        var result = await handler.Handle(
            new GetPlayerSkillDeviationsQuery(userId, ChartType.Single, DifficultyLevel.From(18)),
            CancellationToken.None);

        Assert.True(result.Usable);
        Assert.Equal(6, result.ScoredChartCount);
        Assert.Equal(20_000, result.Skills[Skill.Twists].ScoreDeviation, 3);
        Assert.Equal(-20_000, result.Skills[Skill.Brackets].ScoreDeviation, 3);
        Assert.Equal(0, result.Skills[Skill.Stamina].ScoreDeviation, 3);
        Assert.All(result.Skills.Values, s => Assert.True(s.Usable));
        Assert.All(result.Skills.Values, s => Assert.Equal(2.0, s.Evidence, 3));
    }

    [Fact]
    public async Task FewerThanThreeEvidencedSkillsIsUnusable()
    {
        var (handler, userId) = BuildFixture(includeStaminaChips: false);

        var result = await handler.Handle(
            new GetPlayerSkillDeviationsQuery(userId, ChartType.Single, DifficultyLevel.From(18)),
            CancellationToken.None);

        Assert.False(result.Usable);
        Assert.True(result.Skills[Skill.Twists].Usable);
        Assert.True(result.Skills[Skill.Brackets].Usable);
    }

    [Fact]
    public async Task NoScoresYieldsEmptyUnusableProfile()
    {
        var userId = Guid.NewGuid();
        var chartRepo = new Mock<IChartRepository>();
        chartRepo.Setup(c => c.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
        var scoreReader = new Mock<IScoreReader>();
        scoreReader.Setup(s => s.GetBestScores(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartSkillChipsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, IReadOnlyList<ChartSkillChipRecord>>());

        var handler = new PlayerSkillDeviationsHandler(mediator.Object, chartRepo.Object, scoreReader.Object,
            Mock.Of<IPlayerStatsReader>(), Mock.Of<IUserTierListRepository>(), FakeDateTime.At(Now).Object);

        var result = await handler.Handle(
            new GetPlayerSkillDeviationsQuery(userId, ChartType.Single, DifficultyLevel.From(18)),
            CancellationToken.None);

        Assert.False(result.Usable);
        Assert.Empty(result.Skills);
        Assert.Equal(0, result.ScoredChartCount);
    }
}
