using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetCommunityTierListHandlerTests
{
    [Theory]
    [InlineData(-2.0, TierListCategory.Overrated)]
    [InlineData(-1.0, TierListCategory.Overrated)]
    [InlineData(-.5, TierListCategory.VeryEasy)]
    [InlineData(-.25, TierListCategory.Easy)]
    [InlineData(0, TierListCategory.Medium)]
    [InlineData(.25, TierListCategory.Hard)]
    [InlineData(.5, TierListCategory.VeryHard)]
    [InlineData(1.0, TierListCategory.Underrated)]
    [InlineData(2.0, TierListCategory.Underrated)]
    public async Task BandsVoteAdjustmentsOntoTierListCategories(double adjustment, TierListCategory expected)
    {
        // The stored aggregate is level + .5 + average adjustment (ReCalculateChartRatingHandler).
        var chart = new ChartBuilder().WithMix(MixEnum.Prime).WithLevel(18).Build();
        var handler = BuildHandler(new[] { chart },
            new[] { new ChartDifficultyRatingRecord(chart.Id, 18.5 + adjustment, 1, 0) });

        var result = await handler.Handle(new GetCommunityTierListQuery(MixEnum.Prime), CancellationToken.None);

        Assert.Equal(expected, Assert.Single(result).Category);
    }

    [Fact]
    public async Task ChartsWithoutVotesReadUnrecorded()
    {
        var chart = new ChartBuilder().WithMix(MixEnum.Fiesta).WithLevel(10).Build();
        var handler = BuildHandler(new[] { chart }, Array.Empty<ChartDifficultyRatingRecord>());

        var result = await handler.Handle(new GetCommunityTierListQuery(MixEnum.Fiesta), CancellationToken.None);

        Assert.Equal(TierListCategory.Unrecorded, Assert.Single(result).Category);
    }

    [Fact]
    public async Task OrdersMostUnderratedFirstWithUnrecordedLast()
    {
        var underrated = new ChartBuilder().WithMix(MixEnum.Prime).WithLevel(18).WithSongName("A Underrated").Build();
        var medium = new ChartBuilder().WithMix(MixEnum.Prime).WithLevel(18).WithSongName("B Medium").Build();
        var unrecorded = new ChartBuilder().WithMix(MixEnum.Prime).WithLevel(18).WithSongName("C Unvoted").Build();
        var handler = BuildHandler(new[] { medium, unrecorded, underrated }, new[]
        {
            new ChartDifficultyRatingRecord(medium.Id, 18.5, 1, 0),
            new ChartDifficultyRatingRecord(underrated.Id, 20.5, 1, 0)
        });

        var result = (await handler.Handle(new GetCommunityTierListQuery(MixEnum.Prime), CancellationToken.None))
            .OrderBy(e => e.Order).ToArray();

        Assert.Equal(new[] { underrated.Id, medium.Id, unrecorded.Id }, result.Select(e => e.ChartId));
    }

    private static GetCommunityTierListHandler BuildHandler(IEnumerable<Chart> charts,
        IEnumerable<ChartDifficultyRatingRecord> ratings)
    {
        var ratingsRepo = new Mock<IChartDifficultyRatingRepository>();
        ratingsRepo.Setup(r => r.GetAllChartRatedDifficulties(It.IsAny<MixEnum>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ratings);
        var chartsRepo = new Mock<IChartRepository>();
        chartsRepo.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return new GetCommunityTierListHandler(ratingsRepo.Object, chartsRepo.Object);
    }
}
