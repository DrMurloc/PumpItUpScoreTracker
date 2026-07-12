using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class UserTierListSagaTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task ScoreUpdateMaterializesTheAffectedFolder()
    {
        var chart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { chart });
        var entries = new[]
            { new SongTierListEntry("My Relative Scores", chart.Id, TierListCategory.Easy, 0) };
        var mediator = MediatorReturning(entries);
        var userTierLists = new Mock<IUserTierListRepository>();
        var saga = BuildSaga(charts: charts, mediator: mediator, userTierLists: userTierLists);

        await saga.Consume(BuildContext(ScoresUpdated(chart.Id)));

        mediator.Verify(m => m.Send(
            It.Is<GetMyRelativeTierListQuery>(q =>
                q.ChartType == ChartType.Double && q.Level == DifficultyLevel.From(17) &&
                q.UserId == UserId && q.Mix == MixEnum.Phoenix),
            It.IsAny<CancellationToken>()), Times.Once);
        userTierLists.Verify(r => r.SaveUserFolder(MixEnum.Phoenix, UserId,
            It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(chart.Id)),
            entries, It.IsAny<IReadOnlyDictionary<Guid, double>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MaterializationStampsFolderScopedFreshness()
    {
        // Score-age workshop: freshness normalizes within the player's own folder —
        // the recently-improved chart votes louder than the two-year-old one, and the
        // weights ride along to the repository with the entries.
        var now = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero);
        var freshChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var staleChart = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var charts = ChartsMock(new[] { freshChart, staleChart });
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(freshChart.Id, PhoenixScore.From(950000), null, false, now.AddDays(-7)),
                new RecordedPhoenixScore(staleChart.Id, PhoenixScore.From(950000), null, false, now.AddDays(-730))
            });
        var userTierLists = new Mock<IUserTierListRepository>();
        IReadOnlyDictionary<Guid, double>? saved = null;
        userTierLists.Setup(r => r.SaveUserFolder(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IEnumerable<SongTierListEntry>>(),
                It.IsAny<IReadOnlyDictionary<Guid, double>>(), It.IsAny<CancellationToken>()))
            .Callback((MixEnum _, Guid _, IReadOnlyCollection<Guid> _, IEnumerable<SongTierListEntry> _,
                IReadOnlyDictionary<Guid, double> freshness, CancellationToken _) => saved = freshness)
            .Returns(Task.CompletedTask);
        var saga = BuildSaga(charts: charts, scores: scores, userTierLists: userTierLists,
            clock: FakeDateTime.At(now));

        await saga.Consume(BuildContext(ScoresUpdated(freshChart.Id)));

        Assert.NotNull(saved);
        Assert.True(saved![freshChart.Id] > 1.0, $"fresh entry should vote louder ({saved[freshChart.Id]:0.00})");
        Assert.True(saved[staleChart.Id] < 1.0, $"stale entry should vote quieter ({saved[staleChart.Id]:0.00})");
    }

    [Fact]
    public async Task ScoreUpdateMaterializesEachAffectedFolderOnce()
    {
        var d17A = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var d17B = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var s15 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMock(new[] { d17A, d17B, s15 });
        var mediator = MediatorReturning(Array.Empty<SongTierListEntry>());
        var saga = BuildSaga(charts: charts, mediator: mediator);

        await saga.Consume(BuildContext(ScoresUpdated(d17A.Id, d17B.Id, s15.Id)));

        mediator.Verify(m => m.Send(It.Is<GetMyRelativeTierListQuery>(q =>
                q.ChartType == ChartType.Double && q.Level == DifficultyLevel.From(17)),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<GetMyRelativeTierListQuery>(q =>
                q.ChartType == ChartType.Single && q.Level == DifficultyLevel.From(15)),
            It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ScoreUpdateSkipsCoOpCharts()
    {
        var coOp = new ChartBuilder().WithLevel(3).WithType(ChartType.CoOp).Build();
        var charts = ChartsMock(new[] { coOp });
        var mediator = MediatorReturning(Array.Empty<SongTierListEntry>());
        var userTierLists = new Mock<IUserTierListRepository>();
        var saga = BuildSaga(charts: charts, mediator: mediator, userTierLists: userTierLists);

        await saga.Consume(BuildContext(ScoresUpdated(coOp.Id)));

        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(),
            It.IsAny<CancellationToken>()), Times.Never);
        userTierLists.Verify(r => r.SaveUserFolder(It.IsAny<MixEnum>(), It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IEnumerable<SongTierListEntry>>(),
            It.IsAny<IReadOnlyDictionary<Guid, double>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScoreUpdateFoldsPerformanceChartsIntoTheirBaseFolder()
    {
        var performance = new ChartBuilder().WithLevel(15).WithType(ChartType.SinglePerformance).Build();
        var charts = ChartsMock(new[] { performance });
        var mediator = MediatorReturning(Array.Empty<SongTierListEntry>());
        var saga = BuildSaga(charts: charts, mediator: mediator);

        await saga.Consume(BuildContext(ScoresUpdated(performance.Id)));

        mediator.Verify(m => m.Send(It.Is<GetMyRelativeTierListQuery>(q =>
                q.ChartType == ChartType.Single && q.Level == DifficultyLevel.From(15)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScoreUpdateWithNoChangesDoesNothing()
    {
        var charts = ChartsMock(Array.Empty<Chart>());
        var saga = BuildSaga(charts: charts);

        await saga.Consume(BuildContext(ScoresUpdated()));

        charts.Verify(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(),
            It.IsAny<ChartType?>(), It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackfillMaterializesEveryScoringUsersScoredFolders()
    {
        var d17 = new ChartBuilder().WithLevel(17).WithType(ChartType.Double).Build();
        var s15 = new ChartBuilder().WithLevel(15).WithType(ChartType.Single).Build();
        var charts = ChartsMock(new[] { d17, s15 });
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetActiveUserIds(MixEnum.Phoenix, It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { userA, userB });
        scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, userA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Score(d17.Id), Score(s15.Id) });
        scores.Setup(s => s.GetBestScores(MixEnum.Phoenix, userB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Score(s15.Id) });
        var mediator = MediatorReturning(Array.Empty<SongTierListEntry>());
        var userTierLists = new Mock<IUserTierListRepository>();
        var saga = BuildSaga(charts: charts, mediator: mediator, scores: scores, userTierLists: userTierLists);

        await saga.Consume(BuildContext(new BackfillUserTierListsCommand()));

        // User A scored two folders, user B one — three materializations total.
        userTierLists.Verify(r => r.SaveUserFolder(MixEnum.Phoenix, userA,
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IEnumerable<SongTierListEntry>>(),
            It.IsAny<IReadOnlyDictionary<Guid, double>>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        userTierLists.Verify(r => r.SaveUserFolder(MixEnum.Phoenix, userB,
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<IEnumerable<SongTierListEntry>>(),
            It.IsAny<IReadOnlyDictionary<Guid, double>>(), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.IsAny<GetMyRelativeTierListQuery>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static PlayerScoresUpdatedEvent ScoresUpdated(params Guid[] chartIds)
    {
        return PlayerScoresUpdatedEvent.Create(DateTimeOffset.UnixEpoch, UserId, MixEnum.Phoenix,
            chartIds.Select(id =>
                    new PlayerScoresUpdatedEvent.ScoreChange(id, true, null, 950000, null, false))
                .ToArray());
    }

    private static RecordedPhoenixScore Score(Guid chartId)
    {
        return new RecordedPhoenixScore(chartId, PhoenixScore.From(950000), null, false, DateTimeOffset.UnixEpoch);
    }

    private static Mock<IChartRepository> ChartsMock(IEnumerable<Chart> charts)
    {
        var m = new Mock<IChartRepository>();
        m.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), It.IsAny<DifficultyLevel?>(), It.IsAny<ChartType?>(),
                It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
        return m;
    }

    private static Mock<IMediator> MediatorReturning(IEnumerable<SongTierListEntry> entries)
    {
        var m = new Mock<IMediator>();
        m.Setup(x => x.Send(It.IsAny<GetMyRelativeTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);
        return m;
    }

    private static UserTierListSaga BuildSaga(
        Mock<IChartRepository>? charts = null,
        Mock<IMediator>? mediator = null,
        Mock<IScoreReader>? scores = null,
        Mock<IUserTierListRepository>? userTierLists = null,
        Mock<IDateTimeOffsetAccessor>? clock = null)
    {
        charts ??= ChartsMock(Array.Empty<Chart>());
        mediator ??= MediatorReturning(Array.Empty<SongTierListEntry>());
        scores ??= new Mock<IScoreReader>();
        userTierLists ??= new Mock<IUserTierListRepository>();
        return new UserTierListSaga(charts.Object, mediator.Object, scores.Object, userTierLists.Object,
            (clock ?? FakeDateTime.At(2026, 7, 12)).Object);
    }

    private static ConsumeContext<T> BuildContext<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
