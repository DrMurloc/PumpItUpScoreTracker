using ScoreTracker.OfficialMirror.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

public sealed class WorldRankingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GetAllReturnsRecordedScoresForChartsThatExist()
    {
        var chartId = Guid.NewGuid();
        var chart = new ChartBuilder().WithId(chartId).WithLevel(15)
            .WithType(ChartType.Single).WithSongName("Cool Song").Build();
        var leaderboards = LeaderboardsMock(
            usernames: Array.Empty<string>(),
            statuses: new Dictionary<string, UserOfficialLeaderboard[]>
            {
                ["alice"] = new[] { Status("alice", "Cool Song S15", score: 950000) }
            });
        var charts = ChartsForSongMock(songName: "Cool Song", new[] { chart });

        var result = await BuildService(leaderboards: leaderboards, charts: charts)
            .GetAll(MixEnum.Phoenix, Name.From("alice"), CancellationToken.None);

        var record = Assert.Single(result);
        Assert.Equal(chartId, record.ChartId);
        Assert.Equal((PhoenixScore)950000, record.Score);
    }

    [Fact]
    public async Task GetAllSkipsRecordsWhenNoMatchingChartFound()
    {
        // The chart returned for "Cool Song" is Single 17, but the leaderboard says S15 → no match.
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Single)
            .WithSongName("Cool Song").Build();
        var leaderboards = LeaderboardsMock(
            usernames: Array.Empty<string>(),
            statuses: new Dictionary<string, UserOfficialLeaderboard[]>
            {
                ["alice"] = new[] { Status("alice", "Cool Song S15", score: 950000) }
            });
        var charts = ChartsForSongMock(songName: "Cool Song", new[] { chart });

        var result = await BuildService(leaderboards: leaderboards, charts: charts)
            .GetAll(MixEnum.Phoenix, Name.From("alice"), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllStampsResultsWithCurrentDateTime()
    {
        var chart = new ChartBuilder().WithLevel(15).WithType(ChartType.Single)
            .WithSongName("Cool Song").Build();
        var leaderboards = LeaderboardsMock(
            usernames: Array.Empty<string>(),
            statuses: new Dictionary<string, UserOfficialLeaderboard[]>
            {
                ["alice"] = new[] { Status("alice", "Cool Song S15", score: 950000) }
            });
        var charts = ChartsForSongMock(songName: "Cool Song", new[] { chart });

        var result = await BuildService(leaderboards: leaderboards, charts: charts)
            .GetAll(MixEnum.Phoenix, Name.From("alice"), CancellationToken.None);

        Assert.Equal(Now, result.Single().RecordedDate);
    }

    [Theory]
    [InlineData("Singles", 1, 0)]
    [InlineData("Doubles", 0, 1)]
    [InlineData("All", 1, 1)]
    public async Task GetTop50FiltersChartsByType(string type, int expectedSingleCount, int expectedDoubleCount)
    {
        var single = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).WithSongName("Song A").Build();
        var dbl = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).WithSongName("Song B").Build();
        var leaderboards = LeaderboardsMock(
            usernames: Array.Empty<string>(),
            statuses: new Dictionary<string, UserOfficialLeaderboard[]>
            {
                ["alice"] = new[]
                {
                    Status("alice", "Song A S15", score: 950000),
                    Status("alice", "Song B D17", score: 900000)
                }
            });
        var charts = new Mock<IChartRepository>();
        charts.Setup(c => c.GetChartsForSong(MixEnum.Phoenix, It.Is<Name>(n => (string)n == "Song A"),
                It.IsAny<CancellationToken>())).ReturnsAsync(new[] { single });
        charts.Setup(c => c.GetChartsForSong(MixEnum.Phoenix, It.Is<Name>(n => (string)n == "Song B"),
                It.IsAny<CancellationToken>())).ReturnsAsync(new[] { dbl });

        var result = (await BuildService(leaderboards: leaderboards, charts: charts)
            .GetTop50(MixEnum.Phoenix, Name.From("alice"), type, CancellationToken.None)).ToArray();

        Assert.Equal(expectedSingleCount, result.Count(r => r.ChartId == single.Id));
        Assert.Equal(expectedDoubleCount, result.Count(r => r.ChartId == dbl.Id));
    }

    private static WorldRankingService BuildService(
        Mock<IOfficialLeaderboardRepository>? leaderboards = null,
        Mock<IChartRepository>? charts = null,
        Mock<IDateTimeOffsetAccessor>? dateTime = null)
    {
        leaderboards ??= LeaderboardsMock(usernames: Array.Empty<string>());
        charts ??= new Mock<IChartRepository>();
        dateTime ??= FakeDateTime.At(Now);
        return new WorldRankingService(leaderboards.Object, NullLogger<WorldRankingService>.Instance,
            charts.Object, dateTime.Object);
    }

    private static Mock<IOfficialLeaderboardRepository> LeaderboardsMock(
        IEnumerable<string> usernames,
        IDictionary<string, UserOfficialLeaderboard[]>? statuses = null)
    {
        var m = new Mock<IOfficialLeaderboardRepository>();
        m.Setup(l => l.GetOfficialLeaderboardUsernames(MixEnum.Phoenix, "Chart", It.IsAny<CancellationToken>()))
            .ReturnsAsync(usernames);
        if (statuses != null)
            foreach (var (user, entries) in statuses)
                m.Setup(l => l.GetOfficialLeaderboardStatuses(MixEnum.Phoenix, user, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(entries);
        return m;
    }

    private static Mock<IChartRepository> ChartsForSongMock(string songName, IEnumerable<Chart> result)
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetChartsForSong(MixEnum.Phoenix, It.Is<Name>(n => (string)n == songName),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return m;
    }

    private static UserOfficialLeaderboard Status(string username, string leaderboardName, int score,
        string type = "Chart") =>
        new(Username: username, Place: 1, OfficialLeaderboardType: type,
            LeaderboardName: leaderboardName, Score: score);
}
