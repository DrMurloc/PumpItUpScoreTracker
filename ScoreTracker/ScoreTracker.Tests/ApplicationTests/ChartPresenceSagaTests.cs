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
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
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
///     The PUMBILITY presence census and its read (docs/design/chart-presence-graph.md §2, §7). The
///     counting rules are pinned by <c>ChartPresenceCensusTests</c> and <c>ChartPresenceReadingTests</c>;
///     these cover who the saga reads, how each is placed on the ladder, and what it writes.
/// </summary>
public sealed class ChartPresenceSagaTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 16, 12, 30, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IOfficialPlacementReader> _official = new();
    private readonly Mock<IChartPresenceRepository> _repository = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IPlayerStatsReader> _stats = new();

    [Fact]
    public async Task AccountsStandOnTheirOwnPumbilityAndRankingPlayersOnTheNumberTheRankingPublishes()
    {
        var first = Chart(22);
        var second = Chart(22);
        var alice = Guid.NewGuid();
        Catalog(first, second);
        Accounts((alice, 10_400));
        _scores.Setup(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
                ChartType.Single, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Score(alice, first, 995_000), Score(alice, second, 990_000) });
        // One person on the ranking owning two rows, placed on SILVER by the number published for them.
        Ranking(new BoardPeerReading(new[] { 7, 8 }, "BOB#1234", 12_700, null),
            new BoardScoreReading(7, second.Id, 22, 980_000), new BoardScoreReading(8, second.Id, 22, 995_000));
        _official.Setup(o => o.GetChartsWithBoards(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { second.Id });
        ChartPresenceCensusResult? written = null;
        _repository.Setup(r => r.Replace(MixEnum.Phoenix2, It.IsAny<ChartPresenceCensusResult>(), At,
                It.IsAny<CancellationToken>()))
            .Callback((MixEnum _, ChartPresenceCensusResult census, DateTimeOffset _, CancellationToken _) =>
                written = census)
            .Returns(Task.CompletedTask);

        await Saga().Consume(Context(new RebuildChartPresenceCommand(MixEnum.Phoenix2)));

        Assert.NotNull(written);
        Assert.Equal((1, 1), (written!.Columns[0].Players, written.Columns[0].SitePlayers));
        Assert.Equal((1, 0), (written.Columns[1].Players, written.Columns[1].SitePlayers));
        var bronzeFirst = written.Rows.Single(r => r.ChartId == first.Id && r.Column == 0);
        Assert.Equal((1, false), (bronzeFirst.Holders, bronzeFirst.CountsBoardPlayers));
        Assert.True(written.Rows.Single(r => r.ChartId == second.Id && r.Column == 0).CountsBoardPlayers);
        // Two rows, one person, one hold.
        Assert.Equal(1, written.Rows.Single(r => r.ChartId == second.Id && r.Column == 1).Holders);
    }

    [Fact]
    public async Task NothingIsWrittenWhenNobodyStandsOnTheLadder()
    {
        Catalog(Chart(22));
        Accounts();
        Ranking(null);
        _official.Setup(o => o.GetChartsWithBoards(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());

        await Saga().Consume(Context(new RebuildChartPresenceCommand(MixEnum.Phoenix2)));

        _repository.Verify(r => r.Replace(It.IsAny<MixEnum>(), It.IsAny<ChartPresenceCensusResult>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheReadPlacesTheViewerOnTheirTitleWithTheChartsSpotInTheirOwnTopFifty()
    {
        var chart = Chart(22);
        var harder = Chart(24);
        var viewer = Guid.NewGuid();
        Catalog(chart, harder);
        Census(chart.Id);
        _stats.Setup(s => s.GetStats(MixEnum.Phoenix2, viewer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Stats(viewer, 10_300));
        _scores.Setup(s => s.GetBestScores(MixEnum.Phoenix2, viewer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chart.Id, PhoenixScore.From(995_000), PhoenixPlate.MarvelousGame, false, At),
                new RecordedPhoenixScore(harder.Id, PhoenixScore.From(995_000), PhoenixPlate.MarvelousGame, false, At)
            });

        var presence = await Saga().Handle(new GetChartPumbilityPresenceQuery(chart.Id, MixEnum.Phoenix2, viewer),
            CancellationToken.None);

        Assert.Equal(new PumbilityPresenceViewer(0, 2), presence!.Viewer);
        Assert.Equal(At, presence.ComputedAt);
    }

    [Fact]
    public async Task TheReadAnswersNothingBeforeTheFirstCensus()
    {
        _repository.Setup(r => r.GetColumns(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartPresenceColumns(Array.Empty<ChartPresenceColumnRow>(), null));

        Assert.Null(await Saga().Handle(new GetChartPumbilityPresenceQuery(Guid.NewGuid(), MixEnum.Phoenix2, null),
            CancellationToken.None));
        _repository.Verify(r => r.GetRows(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private ChartPresenceSaga Saga()
    {
        return new ChartPresenceSaga(_repository.Object, _charts.Object, _stats.Object, _scores.Object,
            _official.Object, FakeDateTime.At(At).Object, NullLogger<ChartPresenceSaga>.Instance);
    }

    private void Catalog(params Chart[] charts)
    {
        _charts.Setup(r => r.GetCharts(MixEnum.Phoenix2, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);
    }

    private void Accounts(params (Guid UserId, double Pumbility)[] accounts)
    {
        _stats.Setup(s => s.GetPlayersInPoolBand(MixEnum.Phoenix2, PumbilityPool.Total, 10_000, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts.Select(a => a.UserId).ToArray());
        _stats.Setup(s => s.GetStats(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(accounts.Select(a => Stats(a.UserId, a.Pumbility)).ToArray());
    }

    private void Ranking(BoardPeerReading? player, params BoardScoreReading[] scores)
    {
        _official.Setup(o => o.GetBoardBand(MixEnum.Phoenix2, PumbilityPool.Total, 10_000, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(player == null
                ? null
                : new BoardPeerGroupReading(At, new[] { player }));
        _official.Setup(o => o.GetBoardScores(MixEnum.Phoenix2, ChartType.Single,
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);
        _official.Setup(o => o.GetBoardScores(MixEnum.Phoenix2, ChartType.Double,
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<BoardScoreReading>());
    }

    /// <summary>A census of one BRONZE column holding the chart.</summary>
    private void Census(Guid chartId)
    {
        _repository.Setup(r => r.GetColumns(MixEnum.Phoenix2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartPresenceColumns(
                new[] { new ChartPresenceColumnRow(0, Name.From("[P.B] BRONZE"), 30, 30) }, At));
        _repository.Setup(r => r.GetRows(MixEnum.Phoenix2, chartId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartPresenceRow(chartId, 0, true, 6, new PumbilitySpots(1, 2, 3, 4, 5), Array.Empty<double>(), 0,
                    null)
            });
    }

    private static PlayerStatsRecord Stats(Guid userId, double pumbility)
    {
        return new PlayerStatsRecord(userId, 0, 1, 0, 0, 0, pumbility, 0, 0, 0, 0, 0, 0, 0, 0, 20, 20, 20);
    }

    private static UserPhoenixScore Score(Guid userId, Chart chart, int score)
    {
        return new UserPhoenixScore(userId, chart.Id, "Player", score, PhoenixPlate.MarvelousGame, false);
    }

    private static Chart Chart(int level)
    {
        return new ChartBuilder().WithId(Guid.NewGuid()).WithMix(MixEnum.Phoenix2).WithType(ChartType.Single)
            .WithLevel(level).Build();
    }

    private static ConsumeContext<RebuildChartPresenceCommand> Context(RebuildChartPresenceCommand message)
    {
        var context = new Mock<ConsumeContext<RebuildChartPresenceCommand>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }
}
