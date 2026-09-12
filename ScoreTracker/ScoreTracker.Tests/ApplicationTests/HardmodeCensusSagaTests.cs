using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.ChartIntelligence.Application;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Messages;
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

/// <summary>
///     The weekly Hardmode census (docs/design/hardmode-leaderboard.md §1/§2). The cut rule itself
///     is pinned by <c>HardmodeCutTests</c>; these cover what the saga does around it — who votes,
///     how a chart earns its weight, and what it refuses to write.
/// </summary>
public sealed class HardmodeCensusSagaTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task WritesTheRarestChartsOfEveryFolderAndAnnouncesTheRebuild()
    {
        // One folder of 52 singles and one voter whose fifty never reaches the first two, so
        // those two are unheld. A quarter of 52 is 13, which is more than the two unheld, so the
        // cut is 13 — and the two nobody holds lead it. The count is POOLS, not players: a
        // voter whose fifty is all singles fills their combined pool and their singles pool.
        var charts = Folder(52, ChartType.Single, 21);
        var pool = FullPool(charts.Skip(2));
        var bus = new Mock<IBus>();
        var repository = new Mock<IHardmodeChartRepository>();
        var saga = Build(charts, new[] { (Guid.NewGuid(), pool) }, repository, bus);

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        repository.Verify(r => r.Replace(MixEnum.Phoenix2,
            It.Is<IReadOnlyCollection<HardmodeChartRecord>>(written =>
                written.Count == 13
                && written.All(w => w.FolderSize == 52 && w.FolderCut == 13)
                && written.Take(2).All(w => w.Holders == 0 && w.Points == 0)
                && written.Skip(2).All(w => w.Holders == 1)),
            At, It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(b => b.Publish(
            It.Is<HardmodeChartsRebuiltEvent>(e =>
                e.Mix == MixEnum.Phoenix2 && e.QualifyingCharts == 13 && e.PoolsCounted == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AChartHeldAtTheTopOfAPoolOutweighsOneHeldAtTheBottom()
    {
        // Fifty-one charts in one folder, one voter. The chart left out of the fifty is the
        // rarest; of the fifty, slot 50 is worth 1 and slot 1 is worth 50.
        var charts = Folder(51, ChartType.Single, 21);
        var pool = FullPool(charts.Take(50));
        var repository = new Mock<IHardmodeChartRepository>();
        var saga = Build(charts, new[] { (Guid.NewGuid(), pool) }, repository, new Mock<IBus>());

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        repository.Verify(r => r.Replace(MixEnum.Phoenix2, It.Is<IReadOnlyCollection<HardmodeChartRecord>>(w =>
            // The unheld chart first at zero, then the cheapest slots — 3 points because the
            // combined pool and the singles pool both count the same slot.
            w.First().Points == 0
            && w.Skip(1).First().Points == 2
            && w.All(r2 => r2.Holders <= 1)), At, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ThreePoolsOfOnePlayerCountAsOneHolder()
    {
        // A singles chart sits in that player's combined pool and their singles pool, so it
        // carries two slot weights — and one holder, because the holder is the player.
        var charts = Folder(60, ChartType.Single, 21);
        var pool = FullPool(charts.Take(50));
        var repository = new Mock<IHardmodeChartRepository>();
        var saga = Build(charts, new[] { (Guid.NewGuid(), pool) }, repository, new Mock<IBus>());

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        repository.Verify(r => r.Replace(MixEnum.Phoenix2, It.Is<IReadOnlyCollection<HardmodeChartRecord>>(w =>
            w.All(x => x.Holders <= 1)), At, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APartialPoolDoesNotVote()
    {
        // Three charts in a fifty-slot pool would hand slot 1 fifty points off nothing.
        var charts = Folder(8, ChartType.Single, 21);
        var repository = new Mock<IHardmodeChartRepository>();
        var bus = new Mock<IBus>();
        var saga = Build(charts, new[] { (Guid.NewGuid(), charts.Take(3).ToArray()) }, repository, bus);

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        // No full pool anywhere, so there is no census to take and last week's list stands.
        repository.Verify(r => r.Replace(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeChartRecord>>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        bus.Verify(b => b.Publish(It.IsAny<HardmodeChartsRebuiltEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BoardPlayersVoteAlongsideAccounts()
    {
        var charts = Folder(60, ChartType.Single, 21);
        var repository = new Mock<IHardmodeChartRepository>();
        var bus = new Mock<IBus>();
        var official = new[]
        {
            new OfficialPoolSlots(4242, null, charts.Take(50).Select(c => c.Id).ToArray())
        };
        var saga = Build(charts, Array.Empty<(Guid, Chart[])>(), repository, bus, official);

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        bus.Verify(b => b.Publish(It.Is<HardmodeChartsRebuiltEvent>(e => e.PoolsCounted == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.Replace(MixEnum.Phoenix2, It.Is<IReadOnlyCollection<HardmodeChartRecord>>(w =>
            w.Any(x => x.Holders == 1)), At, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AMixWithNoCatalogWritesNothing()
    {
        var repository = new Mock<IHardmodeChartRepository>();
        var bus = new Mock<IBus>();
        var saga = Build(Array.Empty<Chart>(), Array.Empty<(Guid, Chart[])>(), repository, bus);

        await saga.Consume(Context(new RebuildHardmodeChartsCommand(MixEnum.Phoenix2)));

        repository.Verify(r => r.Replace(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeChartRecord>>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Chart[] Folder(int count, ChartType type, int level)
    {
        return Enumerable.Range(0, count)
            .Select(i => new ChartBuilder().WithSongName($"Chart {i:000}").WithType(type).WithLevel(level)
                .WithMix(MixEnum.Phoenix2).Build())
            .ToArray();
    }

    /// <summary>Fifty charts, descending in score so the pool order is the order given.</summary>
    private static Chart[] FullPool(IEnumerable<Chart> charts)
    {
        return charts.Take(50).ToArray();
    }

    private static HardmodeCensusSaga Build(IReadOnlyCollection<Chart> charts,
        IReadOnlyCollection<(Guid UserId, Chart[] Pool)> pools, Mock<IHardmodeChartRepository> repository,
        Mock<IBus> bus, IReadOnlyList<OfficialPoolSlots>? official = null)
    {
        var chartRepository = new Mock<IChartRepository>();
        chartRepository.Setup(r => r.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);

        var scores = new Mock<IScoreReader>();
        scores.Setup(s => s.GetScores(It.IsAny<MixEnum>(), It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, ChartType type, DifficultyLevel level, CancellationToken _) =>
                pools.SelectMany(p => p.Pool
                        .Where(c => c.Type == type && c.Level == level)
                        // Descending score down the pool, so slot 1 is the first chart given.
                        .Select((c, i) => (p.UserId, Record: new RecordedPhoenixScore(c.Id,
                            PhoenixScore.From(999_000 - i * 1_000), PhoenixPlate.MarvelousGame, false, At))))
                    .ToArray());

        var officialPools = new Mock<IOfficialPoolReader>();
        officialPools.Setup(p => p.GetFullPools(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(official ?? Array.Empty<OfficialPoolSlots>());

        var scoringLevels = new Mock<IChartScoringLevelRepository>();
        scoringLevels.Setup(s => s.GetScoringLevels(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double>());

        repository.Setup(r => r.Replace(It.IsAny<MixEnum>(), It.IsAny<IReadOnlyCollection<HardmodeChartRecord>>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return new HardmodeCensusSaga(repository.Object, scores.Object, officialPools.Object,
            chartRepository.Object, scoringLevels.Object, FakeDateTime.At(At).Object, bus.Object,
            NullLogger<HardmodeCensusSaga>.Instance);
    }

    private static ConsumeContext<RebuildHardmodeChartsCommand> Context(RebuildHardmodeChartsCommand message)
    {
        var context = new Mock<ConsumeContext<RebuildHardmodeChartsCommand>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}
