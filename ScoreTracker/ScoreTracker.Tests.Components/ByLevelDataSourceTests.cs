using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Web.Services.HomeDashboard;
using Xunit;

namespace ScoreTracker.Tests.Components;

/// <summary>
///     The mix-resolved read seam (C3): full catalog joined to the player's attempts,
///     normalized to one BreakdownRecord shape. Phoenix path yields Score/Grade/Plate;
///     the legacy path yields Grade + Pass only. Unplayed catalog charts are included so
///     Completion can count over the whole folder.
/// </summary>
public sealed class ByLevelDataSourceTests
{
    private static readonly DateTimeOffset When = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid User = Guid.NewGuid();

    private static Chart Chart(Guid id, ChartType type, int level, MixEnum mix, int? playerCount = null) =>
        new(id, mix, new Song("Song", SongType.Arcade, new Uri("https://x/y.png"), TimeSpan.FromMinutes(2), "Artist",
            null), type, level, mix, null, null, new HashSet<Skill>(), null, playerCount);

    private static readonly IDateTimeOffsetAccessor Clock =
        Mock.Of<IDateTimeOffsetAccessor>(c => c.Now == new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero));

    private static ByLevelDataSource Source(Mock<IMediator> mediator) =>
        new(mediator.Object, new ChartCatalogCache(mediator.Object), Clock);

    [Fact]
    public async Task PhoenixMapsScoreGradePlateAndIncludesUnplayedCharts()
    {
        var singles = Guid.NewGuid();
        var doubles = Guid.NewGuid();
        var coop = Guid.NewGuid();
        var charts = new List<Chart>
        {
            Chart(singles, ChartType.Single, 20, MixEnum.Phoenix),
            Chart(doubles, ChartType.Double, 20, MixEnum.Phoenix),
            // Real difficulty 15 but a 2-player count — Bucket must follow PlayerCount, not Level.
            Chart(coop, ChartType.CoOp, 15, MixEnum.Phoenix, playerCount: 2)
        };
        var records = new List<RecordedPhoenixScore>
        {
            new(singles, 1_000_000, PhoenixPlate.PerfectGame, false, When),
            new(doubles, 700_000, null, true, When) // a fail
            // coop: no record -> unplayed
        };

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        mediator.Setup(m => m.Send(It.IsAny<GetPhoenixRecordsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(records);

        var (result, scales) = await Source(mediator).Load(User, MixEnum.Phoenix);

        var s = result.Single(r => r.Type == ChartType.Single);
        Assert.True(s is { IsPlayed: true, IsPassed: true });
        Assert.Equal(1_000_000, s.Score);
        Assert.Equal((int)PhoenixLetterGrade.SSSPlus, s.GradeRank);
        Assert.Equal((int)PhoenixPlate.PerfectGame, s.PlateRank);

        var d = result.Single(r => r.Type == ChartType.Double);
        Assert.True(d is { IsPlayed: true, IsPassed: false });
        Assert.Null(d.Score); // a fail contributes no clear-metric
        Assert.Null(d.GradeRank);

        var c = result.Single(r => r.Type == ChartType.CoOp);
        Assert.False(c.IsPlayed);
        Assert.Equal(2, c.Bucket); // player count, not the difficulty level (15)

        Assert.Equal(16, scales.GradeNames.Count); // full Phoenix grade ladder
        Assert.Equal(8, scales.PlateNames.Count);
    }

    [Fact]
    public async Task LegacyMixMapsGradeAndPassOnlyNoScoreNoPlates()
    {
        var chartId = Guid.NewGuid();
        var chart = Chart(chartId, ChartType.Single, 18, MixEnum.XX);
        var attempts = new List<BestXXChartAttempt>
        {
            new(chart, new XXChartAttempt(XXLetterGrade.SSS, false, null, When))
        };

        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Chart> { chart });
        mediator.Setup(m => m.Send(It.IsAny<GetXXBestChartAttemptsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempts);

        var (result, scales) = await Source(mediator).Load(User, MixEnum.XX);

        var r = Assert.Single(result);
        Assert.True(r is { IsPlayed: true, IsPassed: true });
        Assert.Equal((int)XXLetterGrade.SSS, r.GradeRank);
        Assert.Null(r.Score); // legacy scores aren't 1M-normalized
        Assert.Null(r.PlateRank); // legacy has no plates
        Assert.Equal(8, scales.GradeNames.Count); // F..SSS
        Assert.Empty(scales.PlateNames);
    }
}
