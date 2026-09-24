using System;
using ScoreTracker.Domain.Records;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class CompetitiveLevelsTests
{
    private static readonly PlayerStatsRecord Stats = new(Guid.Empty, TotalRating: 0, HighestLevel: 1,
        ClearCount: 0, CoOpRating: 0, CoOpScore: 0, SkillRating: 0, SkillScore: 0, SkillLevel: 0, SinglesRating: 0,
        SinglesScore: 0, SinglesLevel: 0, DoublesRating: 0, DoublesScore: 0, DoublesLevel: 0,
        CompetitiveLevel: 21.9, SinglesCompetitiveLevel: 19.4, DoublesCompetitiveLevel: 23.7);

    [Theory]
    [InlineData(MixEnum.Rise, ChartType.Single, 19)]
    [InlineData(MixEnum.Rise, ChartType.HalfDouble, 23)]
    [InlineData(MixEnum.RiseArcade, ChartType.Double, 23)]
    [InlineData(MixEnum.Phoenix2, ChartType.Single, 19)]
    [InlineData(MixEnum.Phoenix2, ChartType.Double, 23)]
    [InlineData(MixEnum.Phoenix2, ChartType.CoOp, 21)]
    [InlineData(MixEnum.Phoenix, ChartType.DoublePerformance, 21)]
    public void AFolderReadsTheLevelOfTheTypeThatFillsIt(MixEnum mix, ChartType type, int floor)
    {
        Assert.Equal(floor, CompetitiveLevels.Floor(mix, type, Stats));
    }
}
