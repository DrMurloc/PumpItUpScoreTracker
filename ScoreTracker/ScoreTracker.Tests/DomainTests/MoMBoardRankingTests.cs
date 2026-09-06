using System;
using System.Linq;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.DomainTests;

/// <summary>
///     Boards rank sessions, not players (D16), in score order with the earlier publication
///     winning a tie (§1); and Compare lists the charts two sessions share (§11.3).
/// </summary>
public sealed class MoMBoardRankingTests
{
    private sealed record Row(Guid SessionId, Guid UserId, int Total, DateTimeOffset PublishedAt);

    private static readonly DateTimeOffset Feb = new(2025, 2, 14, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnePlayerMayHoldSeveralPlacesAndATieGoesToTheEarlierSession()
    {
        var tieny = Guid.NewGuid();
        var kim = Guid.NewGuid();
        var rows = new[]
        {
            new Row(Guid.NewGuid(), tieny, 52979, Feb.AddDays(3)),
            new Row(Guid.NewGuid(), kim, 59319, Feb),
            new Row(Guid.NewGuid(), tieny, 41780, Feb.AddDays(20)),
            new Row(Guid.NewGuid(), Guid.NewGuid(), 52979, Feb.AddDays(1))
        };

        var ranked = MoMBoardRanking.Order(rows, r => r.Total, r => r.PublishedAt);

        Assert.Equal(kim, ranked[0].UserId);
        Assert.Equal(Feb.AddDays(1), ranked[1].PublishedAt); // the earlier 52,979
        Assert.Equal(tieny, ranked[2].UserId);
        Assert.Equal(tieny, ranked[3].UserId);
        Assert.Equal(1, MoMBoardRanking.SessionNumber(rows, ranked[2], r => r.UserId, r => r.PublishedAt));
        Assert.Equal(2, MoMBoardRanking.SessionNumber(rows, ranked[3], r => r.UserId, r => r.PublishedAt));
    }

    [Fact]
    public void ALeverPlaceIsOneMoreThanTheSessionsThatBeatIt()
    {
        var board = new[] { 39.0, 36, 31, 31, 36, 30 };
        Assert.Equal(1, MoMBoardRanking.LeverPlace(39, board, higherIsBetter: true));
        Assert.Equal(2, MoMBoardRanking.LeverPlace(36, board, higherIsBetter: true)); // both 36s share 2nd
        Assert.Equal(4, MoMBoardRanking.LeverPlace(31, board, higherIsBetter: true));
        // Downtime: less is better, so 1324 seconds against a board of longer rests is first.
        Assert.Equal(1, MoMBoardRanking.LeverPlace(1324, new[] { 1324.0, 1619, 2105, 2235 }, higherIsBetter: false));
        Assert.Equal(4, MoMBoardRanking.LeverPlace(2235, new[] { 1324.0, 1619, 2105, 2235 }, higherIsBetter: false));
    }

    [Fact]
    public void SharedChartsAreTheOnesBothSessionsPlayedWorstGapFirst()
    {
        var mine = MoMRealSessions.Winter2025();
        // yimmythe42's real overlap with 김재현: Gargoyle cost him 1,892 on a board he won by 1,994.
        var gargoyle = MoMRealSessions.Chart("Gargoyle - FULL SONG -", 25, 378);
        var slam = MoMRealSessions.Chart("Slam", 24, 99);
        var theirs = new[]
        {
            new MoMSessionChart(gargoyle, 924890, PhoenixPlate.RoughGame, false, 5099, 0, 25.5, null),
            new MoMSessionChart(slam, 983047, PhoenixPlate.RoughGame, false, 1622, 0, 24.5, null),
            new MoMSessionChart(MoMRealSessions.Chart("Not in his session", 24, 100), 990000, PhoenixPlate.RoughGame, false, 1500, 0, 24.5, null)
        };

        var shared = MoMCompare.Shared(mine, theirs, worstFirst: true);

        Assert.Equal(2, shared.Count);
        Assert.Equal("Gargoyle - FULL SONG -", shared[0].Chart.Song.Name.ToString());
        Assert.Equal(3207 - 5099, shared[0].Gap);
        Assert.Equal(1528 - 1622, shared[1].Gap);

        var gainsFirst = MoMCompare.Shared(mine, theirs, worstFirst: false);
        Assert.Equal("Slam", gainsFirst[0].Chart.Song.Name.ToString());
    }

    [Fact]
    public void TheSevenChartsInBothOfHisSeasonsJoinOnChartIdentity()
    {
        var now = MoMRealSessions.Winter2025();
        var then = MoMRealSessions.August2024();

        var shared = MoMCompare.Shared(now, then, worstFirst: false);

        Assert.Equal(7, shared.Count);
        // Kasou Shinja moved the most: 1,380 then, 1,722 now.
        Assert.Equal("Kasou Shinja", shared[0].Chart.Song.Name.ToString());
        Assert.Equal(1722 - 1380, shared[0].Gap);
        Assert.Contains(shared, s => s.Chart.Song.Name == "MURDOCH");
        Assert.DoesNotContain(shared, s => s.Chart.Song.Name == "Slam");
    }
}
