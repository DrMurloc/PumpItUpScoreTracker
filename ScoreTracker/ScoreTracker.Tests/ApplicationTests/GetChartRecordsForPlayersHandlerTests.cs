using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.ScoreLedger.Application;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.ScoreLedger.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class GetChartRecordsForPlayersHandlerTests
{
    private static readonly Guid ChartId = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static (Guid UserId, RecordedPhoenixScore Record) Best(Guid userId, int score)
    {
        return (userId, new RecordedPhoenixScore(ChartId, PhoenixScore.From(score), PhoenixPlate.SuperbGame, false, At,
            "officialImport"));
    }

    /// <summary>
    ///     The whole point of the read: the caller hands in the accounts its credential may see,
    ///     and nobody else's best ever leaves the handler — the share gate is the id list.
    /// </summary>
    [Fact]
    public async Task ReturnsOnlyTheNamedPlayersBests()
    {
        var shared = Guid.NewGuid();
        var alsoShared = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var records = new Mock<IPhoenixRecordRepository>();
        records.Setup(r => r.GetRecordedScoresForChart(MixEnum.Phoenix2, ChartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Best(shared, 990000), Best(stranger, 999000), Best(alsoShared, 950000) });
        var handler = new GetChartRecordsForPlayersHandler(records.Object);

        var result = await handler.Handle(
            new GetChartRecordsForPlayersQuery(MixEnum.Phoenix2, ChartId, new[] { shared, alsoShared, Guid.NewGuid() }),
            CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.UserId == stranger);
        Assert.Equal(990000, (int)result.Single(r => r.UserId == shared).Record.Score!.Value);
        Assert.Equal("officialImport", result[0].Record.Source);
    }

    [Fact]
    public async Task NoPlayersMeansNoReadAtAll()
    {
        var records = new Mock<IPhoenixRecordRepository>();
        var handler = new GetChartRecordsForPlayersHandler(records.Object);

        var result = await handler.Handle(
            new GetChartRecordsForPlayersQuery(MixEnum.Phoenix2, ChartId, Array.Empty<Guid>()), CancellationToken.None);

        Assert.Empty(result);
        records.Verify(r => r.GetRecordedScoresForChart(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
