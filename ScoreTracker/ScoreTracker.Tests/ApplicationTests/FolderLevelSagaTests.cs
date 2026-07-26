using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Messages;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.PlayerProgress.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class FolderLevelSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    private static ConsumeContext<BackfillFolderLevelsCommand> Context()
    {
        var ctx = new Mock<ConsumeContext<BackfillFolderLevelsCommand>>();
        ctx.SetupGet(c => c.Message).Returns(new BackfillFolderLevelsCommand());
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private sealed class SagaContext
    {
        public Mock<IChartRepository> Charts { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerFolderLevelRepository> FolderLevels { get; } = new();
        public FolderLevelSaga Saga { get; }

        public SagaContext()
        {
            Charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
            Scores.Setup(s => s.GetActiveUserIds(It.IsAny<MixEnum>(), It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HashSet<Guid>());
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<RecordedPhoenixScore>());
            Saga = new FolderLevelSaga(Charts.Object, Scores.Object, FolderLevels.Object,
                FakeDateTime.At(Now).Object, NullLogger<FolderLevelSaga>.Instance);
        }

        public void GivenCharts(MixEnum mix, params Chart[] charts)
        {
            Charts.Setup(c => c.GetCharts(mix, It.IsAny<DifficultyLevel?>(),
                    It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
        }

        public void GivenPlayers(MixEnum mix, params Guid[] userIds)
        {
            Scores.Setup(s => s.GetActiveUserIds(mix, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(userIds.ToHashSet());
        }

        public void GivenBests(MixEnum mix, Guid userId, params RecordedPhoenixScore[] bests)
        {
            Scores.Setup(s => s.GetBestScores(mix, userId, It.IsAny<CancellationToken>())).ReturnsAsync(bests);
        }
    }

    private static RecordedPhoenixScore Best(Chart chart, int score, bool isBroken = false) =>
        new(chart.Id, PhoenixScore.From(score), PhoenixPlate.FairGame, isBroken, Now);

    [Fact]
    public async Task BackfillStoresAStandingForEveryActivePlayer()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var other = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var ctx = new SagaContext();
        ctx.GivenCharts(MixEnum.Phoenix, chart, other);
        ctx.GivenPlayers(MixEnum.Phoenix, alice, bob);
        ctx.GivenBests(MixEnum.Phoenix, alice, Best(chart, 930000));
        ctx.GivenBests(MixEnum.Phoenix, bob, Best(chart, 950000), Best(other, 950000));

        await ctx.Saga.Consume(Context());

        ctx.FolderLevels.Verify(f => f.Save(alice,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Played == 1 && l.Single().Size == 2),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        ctx.FolderLevels.Verify(f => f.Save(bob,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().IsLamped && l.Single().AverageScore == 950000),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BackfillCoversBothMixesInOnePass()
    {
        var phoenixChart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var phoenix2Chart = new ChartBuilder().WithType(ChartType.Double).WithLevel(18).Build();
        var userId = Guid.NewGuid();
        var ctx = new SagaContext();
        ctx.GivenCharts(MixEnum.Phoenix, phoenixChart);
        ctx.GivenCharts(MixEnum.Phoenix2, phoenix2Chart);
        ctx.GivenPlayers(MixEnum.Phoenix, userId);
        ctx.GivenPlayers(MixEnum.Phoenix2, userId);
        ctx.GivenBests(MixEnum.Phoenix, userId, Best(phoenixChart, 930000));
        ctx.GivenBests(MixEnum.Phoenix2, userId, Best(phoenix2Chart, 930000));

        await ctx.Saga.Consume(Context());

        ctx.FolderLevels.Verify(f => f.Save(userId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Folder == "S22"),
            Now, It.IsAny<CancellationToken>()), Times.Once);
        ctx.FolderLevels.Verify(f => f.Save(userId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Folder == "D18"),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FoldersThePlayerHasNeverTouchedAreNotStored()
    {
        var played = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var untouched = new ChartBuilder().WithType(ChartType.Single).WithLevel(25).Build();
        var userId = Guid.NewGuid();
        var ctx = new SagaContext();
        ctx.GivenCharts(MixEnum.Phoenix, played, untouched);
        ctx.GivenPlayers(MixEnum.Phoenix, userId);
        ctx.GivenBests(MixEnum.Phoenix, userId, Best(played, 930000));

        await ctx.Saga.Consume(Context());

        ctx.FolderLevels.Verify(f => f.Save(userId,
            It.Is<IEnumerable<FolderLevelRecord>>(l => l.Single().Folder == "S22"),
            Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnePlayerFailingDoesNotAbandonTheSweep()
    {
        var chart = new ChartBuilder().WithType(ChartType.Single).WithLevel(22).Build();
        var broken = Guid.NewGuid();
        var fine = Guid.NewGuid();
        var ctx = new SagaContext();
        ctx.GivenCharts(MixEnum.Phoenix, chart);
        ctx.GivenPlayers(MixEnum.Phoenix, broken, fine);
        ctx.GivenBests(MixEnum.Phoenix, fine, Best(chart, 930000));
        ctx.Scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, broken, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("this player is a mess"));

        await ctx.Saga.Consume(Context());

        ctx.FolderLevels.Verify(f => f.Save(fine, It.IsAny<IEnumerable<FolderLevelRecord>>(), Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMixWithNoChartsIsSkippedRatherThanSweptEmpty()
    {
        var userId = Guid.NewGuid();
        var ctx = new SagaContext();
        ctx.GivenPlayers(MixEnum.Phoenix, userId);
        ctx.GivenPlayers(MixEnum.Phoenix2, userId);

        await ctx.Saga.Consume(Context());

        ctx.FolderLevels.Verify(f => f.Save(It.IsAny<Guid>(), It.IsAny<IEnumerable<FolderLevelRecord>>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.Scores.Verify(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheQueryReturnsTheStoredStandings()
    {
        var userId = Guid.NewGuid();
        var stored = new[]
        {
            new FolderLevelRecord(MixEnum.Phoenix, ChartType.Single, DifficultyLevel.From(22), 97, 90, 934245)
        };
        var ctx = new SagaContext();
        ctx.FolderLevels.Setup(f => f.GetFolderLevels(MixEnum.Phoenix, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var result = await ctx.Saga.Handle(new GetPlayerFolderLevelsQuery(userId), CancellationToken.None);

        var folder = Assert.Single(result);
        Assert.Equal("S22", folder.Folder);
        Assert.Equal(92, folder.CompletionPercent);
    }
}
