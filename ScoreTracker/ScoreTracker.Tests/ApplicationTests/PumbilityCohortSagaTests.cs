using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Models.Titles.Phoenix2;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.Services;
using ScoreTracker.PlayerProgress.Application;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The Breakdown card's cohort (docs/design/pumbility-overhaul.md D68): the players holding the
///     viewer's title, folded from the ladder's own read and the official board, with the viewer's
///     own fifty laid over it.
/// </summary>
public sealed class PumbilityCohortSagaTests
{
    private const double Diamond = 17_200;

    [Fact]
    public async Task TheCohortIsEveryoneStandingOnYourBandAndYourFiftyLiesOverIt()
    {
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithChart(out var mine, ChartType.Single, 22)
            .WithOwnPool(Diamond);
        // Twenty-six holders on the level: enough that it is read rather than the gem around it.
        // Half of them hold the staple twice over, the rest once, and the viewer holds it once and
        // a 22 nobody else does.
        for (var i = 0; i < 26; i++) ctx.WithHolder(out _, (staple, 990_000), (mine, i < 13 ? 985_000 : 0));
        ctx.WithOwnScore(staple, 970_000).WithOwnScore(mine, 995_000);

        var record = await ctx.Handle();

        Assert.Equal(PumbilityBand.LevelOf(PumbilityPool.Total, Diamond)!.Name, record.Band);
        Assert.Equal(26, record.Holders);
        Assert.Equal(0, record.BoardHolders);
        var column = Assert.Single(record.Spread.Columns, c => c is { Level: 21, Type: ChartType.Single });
        Assert.Equal(26, column.Holding);
        Assert.Equal(1, column.Mine);
        Assert.Equal(13, Assert.Single(record.Spread.Columns, c => c is { Level: 22, Type: ChartType.Single })
            .Holding);
        // One read per chart type the pool holds, and one only — the cohort is the same answer for
        // everyone standing on it, so a second reader of the same band reads nothing.
        await ctx.Handle();
        ctx.Scores.Verify(s => s.GetPlayerScoresInLevelRange(MixEnum.Phoenix2, It.IsAny<IEnumerable<Guid>>(),
            ChartType.Single, It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ABoardPlayerStandsInTheCohortBesideTheSiteAccounts()
    {
        var gem = PumbilityBand.GemOf(Diamond)!;
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithOwnPool(Diamond)
            .WithHolder(out _, gem, (staple, 990_000))
            .WithBoardHolder(11, "STRANGER#0011", Diamond, (staple, 995_000));

        var record = await ctx.Handle(band: gem.Name);

        Assert.Equal(2, record.Holders);
        Assert.Equal(1, record.BoardHolders);
        Assert.Equal(2, Assert.Single(record.Spread.Columns).Holding);
        Assert.Equal(CohortContext.Swept, record.BoardAsOf);
    }

    [Fact]
    public async Task ALevelTooThinToReadGivesWayToTheGemAroundIt()
    {
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithOwnPool(Diamond);
        var level = PumbilityBand.LevelOf(PumbilityPool.Total, Diamond)!;
        var gem = PumbilityBand.GemOf(Diamond)!;
        var neighbour = PumbilityBand.Levels(PumbilityPool.Total)
            .First(b => b.Gem == gem.Name && b.Name != level.Name);
        // Twenty-four on the viewer's own level — one short of the twenty-five a level owes — and
        // thirty more elsewhere in the gem it sits in, which is what the card ends up reading.
        for (var i = 0; i < 24; i++) ctx.WithHolder(out _, level, (staple, 990_000));
        for (var i = 0; i < 30; i++) ctx.WithHolder(out _, neighbour, (staple, 990_000));

        var record = await ctx.Handle();

        Assert.Equal(gem.Name, record.Band);
        Assert.Equal(54, record.Holders);
    }

    [Fact]
    public async Task ATypedRungIsTheCohortHoweverFewStandOnIt()
    {
        // There is nothing coarser inside a [S] ladder to fall back to, so a rung of six reads as a
        // rung of six rather than widening into something it is not.
        var rung = PumbilityBand.Levels(PumbilityPool.Singles)[10];
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithOwnPool(Diamond, rung.Floor);
        for (var i = 0; i < 6; i++) ctx.WithHolder(out _, rung, (staple, 990_000));

        var record = await ctx.Handle(PumbilityPool.Singles);

        Assert.Equal(rung.Name, record.Band);
        Assert.Equal(6, record.Holders);
        // One type only, so no second column and no split to draw beneath it.
        Assert.All(record.Spread.Columns, c => Assert.Equal(ChartType.Single, c.Type));
        Assert.Null(record.Split);
    }

    [Fact]
    public async Task PhoenixOneReadsTheDifficultyTitleAlreadyStoredForYou()
    {
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithOwnTitle("SS rating on level 21")
            .WithTitleHolder(out _, "SS rating on level 21", (staple, 990_000))
            .WithTitleHolder(out _, "SS rating on level 21", (staple, 985_000));

        var record = await ctx.Handle(mix: MixEnum.Phoenix);

        Assert.Equal("SS rating on level 21", record.Band?.ToString());
        Assert.Equal(2, record.Holders);
        // A Phoenix 1 board row is a PUMBILITY number and a difficulty title is rating earned on one
        // level, so no total can be banded into it — the mirror is never asked.
        Assert.Equal(0, record.BoardHolders);
        ctx.Official.Verify(o => o.GetBoardBand(It.IsAny<MixEnum>(), It.IsAny<PumbilityPool>(), It.IsAny<double>(),
            It.IsAny<double?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheMergedPoolCarriesTheCohortsFiftySplitByType()
    {
        // One holder with a full merged fifty — twenty-eight singles and twenty-two doubles, drawn
        // across both types as one fifty rather than two. Only a full fifty averages honestly, so a
        // holder short of one is counted nowhere in the split.
        var gem = PumbilityBand.GemOf(Diamond)!;
        var ctx = new CohortContext().WithOwnPool(Diamond);
        var pool = new List<(Chart Chart, int Score)>();
        for (var i = 0; i < 28; i++)
        {
            ctx.WithChart(out var chart, ChartType.Single, 21);
            pool.Add((chart, 990_000));
        }

        for (var i = 0; i < 22; i++)
        {
            ctx.WithChart(out var chart, ChartType.Double, 21);
            pool.Add((chart, 990_000));
        }

        ctx.WithHolder(out _, gem, pool.ToArray());
        ctx.WithChart(out var lonely, ChartType.Single, 21).WithHolder(out _, gem, (lonely, 995_000));

        var record = await ctx.Handle(band: gem.Name);

        Assert.Equal(2, record.Holders);
        Assert.Equal(1, record.Split!.Peers);
        Assert.Equal(28, record.Split.SinglesCount);
        Assert.Equal(22, record.Split.DoublesCount);
    }

    [Fact]
    public async Task AViewerUnderTheLadderHasNoBandToCompareAgainst()
    {
        var ctx = new CohortContext().WithChart(out _, ChartType.Single, 21).WithOwnPool(1_000);

        var record = await ctx.Handle();

        Assert.Null(record.Band);
        Assert.Equal(0, record.Holders);
        Assert.Empty(record.Spread.Columns);
    }

    [Fact]
    public async Task PickingABandReadsThatOneInsteadOfYourOwn()
    {
        var ctx = new CohortContext()
            .WithChart(out var staple, ChartType.Single, 21)
            .WithOwnPool(Diamond);
        var bronze = PumbilityBand.Gems().First();
        ctx.WithHolder(out _, bronze, (staple, 950_000));

        var record = await ctx.Handle(band: bronze.Name);

        Assert.Equal(bronze.Name, record.Band);
        Assert.Equal(1, record.Holders);
    }

    /// <summary>
    ///     A mocked port stack and a cohort built over it: the ladder's read answers whoever was
    ///     seated on the band asked for, and the score read answers what they hold.
    /// </summary>
    private sealed class CohortContext
    {
        public static readonly DateTimeOffset Swept = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);

        private readonly List<Chart> _charts = new();
        private readonly Dictionary<(PumbilityPool Pool, string Band), List<Guid>> _holders = new();
        private readonly List<(int PlayerId, string Tag, double Pool)> _onBoard = new();
        private readonly List<BoardScoreReading> _boardScores = new();
        private readonly List<UserPhoenixScore> _theirScores = new();
        private readonly Dictionary<string, List<Guid>> _titleHolders = new();
        private readonly List<RecordedPhoenixScore> _myScores = new();
        private double _doubles;
        private Name? _myTitle;
        private double _singles;
        private double _total;

        public CohortContext()
        {
            Mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _charts.ToArray().AsEnumerable());
            Stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new PlayerStatsRecord(UserId, 0, 1, 0, 0, 0, _total, 0, 0,
                    _singles, 0, 0, _doubles, 0, 0, 20, 20, 20));
            Stats.Setup(s => s.GetPlayersInPoolBand(It.IsAny<MixEnum>(), It.IsAny<PumbilityPool>(),
                    It.IsAny<double>(), It.IsAny<double?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, PumbilityPool pool, double floor, double? ceiling, CancellationToken _) =>
                    Seated(pool, floor, ceiling));
            Titles.Setup(t => t.GetHighestTitle(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _myTitle);
            Titles.Setup(t => t.GetUserIdsWithHighestTitle(It.IsAny<MixEnum>(), It.IsAny<Name>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, Name title, CancellationToken _) =>
                    _titleHolders.GetValueOrDefault(title.ToString(), new List<Guid>()).ToArray().AsEnumerable());
            Scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _myScores.ToArray().AsEnumerable());
            Scores.Setup(s => s.GetPlayerScoresInLevelRange(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                    It.IsAny<ChartType>(), It.IsAny<DifficultyLevel>(), It.IsAny<DifficultyLevel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, IEnumerable<Guid> ids, ChartType type, DifficultyLevel _,
                    DifficultyLevel _, CancellationToken _) =>
                {
                    var asked = ids.ToHashSet();
                    return _theirScores
                        .Where(s => asked.Contains(s.UserId) && ChartOf(s.ChartId).Type == type)
                        .ToArray().AsEnumerable();
                });
            Official.Setup(o => o.GetBoardBand(It.IsAny<MixEnum>(), It.IsAny<PumbilityPool>(), It.IsAny<double>(),
                    It.IsAny<double?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, PumbilityPool _, double floor, double? ceiling, CancellationToken _) =>
                    new BoardPeerGroupReading(Swept, _onBoard
                        .Where(p => p.Pool >= floor && (ceiling is not { } top || p.Pool < top))
                        .Select(p => new BoardPeerReading(new[] { p.PlayerId }, p.Tag, p.Pool, null))
                        .ToArray()));
            Official.Setup(o => o.GetBoardScores(It.IsAny<MixEnum>(), It.IsAny<ChartType>(),
                    It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<int>(), It.IsAny<int>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((MixEnum _, ChartType type, IReadOnlyCollection<int> ids, int _, int _,
                        CancellationToken _) =>
                    _boardScores.Where(r => ids.Contains(r.BoardPlayerId) && ChartOf(r.ChartId).Type == type)
                        .ToArray());
        }

        public Guid UserId { get; } = Guid.NewGuid();
        public Mock<IMediator> Mediator { get; } = new();
        public Mock<IScoreReader> Scores { get; } = new();
        public Mock<IPlayerStatsReader> Stats { get; } = new();
        public Mock<ITitleRepository> Titles { get; } = new();
        public Mock<IOfficialPlacementReader> Official { get; } = NoBoard.Mock();

        private PumbilityCohortSaga Saga { get; set; } = null!;

        public Task<PumbilityCohortRecord> Handle(PumbilityPool pool = PumbilityPool.Total,
            MixEnum mix = MixEnum.Phoenix2, Name? band = null)
        {
            // One saga across a context's calls, so the cache inside it is the thing under test when
            // a band is read twice.
            Saga ??= new PumbilityCohortSaga(Mediator.Object, new PumbilityCohortCache(), Scores.Object,
                Stats.Object, Titles.Object, Official.Object);
            return Saga.Handle(new GetPumbilityTitleCohortQuery(UserId, mix, pool, band), CancellationToken.None);
        }

        public CohortContext WithChart(out Chart chart, ChartType type, int level)
        {
            chart = new ChartBuilder().WithType(type).WithLevel(level).WithMix(MixEnum.Phoenix2).Build();
            _charts.Add(chart);
            return this;
        }

        /// <summary>The viewer's own pools, which is what band they stand on.</summary>
        public CohortContext WithOwnPool(double total, double singles = 0, double doubles = 0)
        {
            _total = total;
            _singles = singles;
            _doubles = doubles;
            return this;
        }

        public CohortContext WithOwnTitle(string title)
        {
            _myTitle = Name.From(title);
            return this;
        }

        public CohortContext WithOwnScore(Chart chart, int score)
        {
            _myScores.Add(new RecordedPhoenixScore(chart.Id, PhoenixScore.From(score), PhoenixPlate.MarvelousGame,
                false, Swept));
            return this;
        }

        /// <summary>A player seated on the viewer's own band, holding the scores given.</summary>
        public CohortContext WithHolder(out Guid holder, params (Chart Chart, int Score)[] scores)
        {
            return WithHolder(out holder, PumbilityBand.LevelOf(PumbilityPool.Total, _total)!, scores);
        }

        public CohortContext WithHolder(out Guid holder, PumbilityBand band, params (Chart Chart, int Score)[] scores)
        {
            holder = Guid.NewGuid();
            Seat(band.Pool, band.Name, holder);
            foreach (var (chart, score) in scores.Where(s => s.Score > 0)) Held(holder, chart, score);
            return this;
        }

        /// <summary>A player whose highest Phoenix 1 difficulty title is this one.</summary>
        public CohortContext WithTitleHolder(out Guid holder, string title, params (Chart Chart, int Score)[] scores)
        {
            holder = Guid.NewGuid();
            if (!_titleHolders.TryGetValue(title, out var seated)) _titleHolders[title] = seated = new List<Guid>();
            seated.Add(holder);
            foreach (var (chart, score) in scores) Held(holder, chart, score);
            return this;
        }

        /// <summary>A player the official board is the only record of, and what it published for them.</summary>
        public CohortContext WithBoardHolder(int playerId, string tag, double pool,
            params (Chart Chart, int Score)[] scores)
        {
            _onBoard.Add((playerId, tag, pool));
            foreach (var (chart, score) in scores)
                _boardScores.Add(new BoardScoreReading(playerId, chart.Id, (int)chart.Level, score));
            return this;
        }

        private void Held(Guid holder, Chart chart, int score)
        {
            _theirScores.Add(new UserPhoenixScore(holder, chart.Id, Name.From("Holder"), PhoenixScore.From(score),
                PhoenixPlate.MarvelousGame, false));
        }

        private void Seat(PumbilityPool pool, Name band, Guid holder)
        {
            var key = (pool, band.ToString());
            if (!_holders.TryGetValue(key, out var seated)) _holders[key] = seated = new List<Guid>();
            seated.Add(holder);
        }

        /// <summary>Everyone seated on a band of the ladder, read the way the SQL does: half-open.</summary>
        private IEnumerable<Guid> Seated(PumbilityPool pool, double floor, double? ceiling)
        {
            return _holders.Where(kv => kv.Key.Pool == pool)
                .Where(kv => Match(pool, kv.Key.Band, floor, ceiling))
                .SelectMany(kv => kv.Value)
                .Distinct()
                .ToArray();
        }

        private static bool Match(PumbilityPool pool, string band, double floor, double? ceiling)
        {
            var seated = PumbilityBand.ByName(pool, Name.From(band))!;
            return seated.Floor >= floor && (ceiling is not { } top || seated.Floor < top);
        }

        private Chart ChartOf(Guid id)
        {
            return _charts.First(c => c.Id == id);
        }
    }
}
