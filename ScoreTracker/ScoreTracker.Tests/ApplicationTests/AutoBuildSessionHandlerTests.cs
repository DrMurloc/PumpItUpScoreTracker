using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Application.Handlers;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class AutoBuildSessionHandlerTests
{
    private static Chart ChartWithDuration(TimeSpan duration, int level = 20)
    {
        var song = new Song(Name.From($"song-{Guid.NewGuid()}"), SongType.Arcade,
            new Uri("https://example.invalid/s.png"), duration, Name.From("artist"), Bpm: null);
        return new ChartBuilder().WithSong(song).WithLevel(level).Build();
    }

    [Fact]
    public async Task BuildsSessionFromScoredChartsWhileRespectingCanAddAndMinimumRest()
    {
        var chart = ChartWithDuration(TimeSpan.FromMinutes(2));
        var charts = new Mock<IChartRepository>();
        var phoenixRecords = new Mock<IPhoenixRecordRepository>();
        var configuration = new TournamentConfiguration(ScoringConfiguration.PumbilityScoring(false))
        {
            MaxTime = TimeSpan.FromMinutes(60)
        };
        var userId = Guid.NewGuid();

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        phoenixRecords.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 980000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow)
            });

        var handler = new AutoBuildSessionHandler(charts.Object, phoenixRecords.Object);

        var session = await handler.Handle(
            new AutoBuildSessionQuery(configuration, userId, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Single(session.Entries);
        Assert.Equal(chart.Id, session.Entries[0].Chart.Id);
    }

    [Fact]
    public async Task SkipsRecordsWithoutScoreOrPlate()
    {
        var chartA = ChartWithDuration(TimeSpan.FromMinutes(2));
        var chartB = ChartWithDuration(TimeSpan.FromMinutes(2));
        var charts = new Mock<IChartRepository>();
        var phoenixRecords = new Mock<IPhoenixRecordRepository>();
        var configuration = new TournamentConfiguration(ScoringConfiguration.PumbilityScoring(false))
        {
            MaxTime = TimeSpan.FromMinutes(60)
        };
        var userId = Guid.NewGuid();

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chartA, chartB });
        phoenixRecords.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chartA.Id, null, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow),
                new RecordedPhoenixScore(chartB.Id, 950000, null, false, DateTimeOffset.UtcNow)
            });

        var handler = new AutoBuildSessionHandler(charts.Object, phoenixRecords.Object);

        var session = await handler.Handle(
            new AutoBuildSessionQuery(configuration, userId, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Empty(session.Entries);
    }

    [Fact]
    public async Task ReturnsEmptySessionWhenMinimumRestIsLargerThanRemainingBudget()
    {
        var chart = ChartWithDuration(TimeSpan.FromMinutes(2));
        var charts = new Mock<IChartRepository>();
        var phoenixRecords = new Mock<IPhoenixRecordRepository>();
        var configuration = new TournamentConfiguration(ScoringConfiguration.PumbilityScoring(false))
        {
            MaxTime = TimeSpan.FromMinutes(3)
        };
        var userId = Guid.NewGuid();

        charts.Setup(r => r.GetCharts(MixEnum.Phoenix, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        phoenixRecords.Setup(r => r.GetRecordedScores(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, 980000, PhoenixPlate.PerfectGame, false, DateTimeOffset.UtcNow)
            });

        var handler = new AutoBuildSessionHandler(charts.Object, phoenixRecords.Object);

        // 3 min budget − 2 min chart = 1 min rest; require 5 min so the only candidate is rejected.
        var session = await handler.Handle(
            new AutoBuildSessionQuery(configuration, userId, TimeSpan.FromMinutes(5)),
            CancellationToken.None);

        Assert.Empty(session.Entries);
    }
}
