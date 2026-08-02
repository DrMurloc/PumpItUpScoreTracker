using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.PlayerProgress.Contracts;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.ScoreLedger.Contracts;
using ScoreTracker.ScoreLedger.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Web.Services;
using Xunit;

namespace ScoreTracker.Tests.Components;

public sealed class SessionBreakdownBuilderTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AChartPlayedSeveralTimesInOneSessionBuildsOneBoard()
    {
        // The shape a session with attempts always has: six losing plays and the clear that
        // ended them, all on one chart. Treating the row list as one-per-chart threw here.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = Enumerable.Range(0, 6)
            .Select(i => Row(chart.Id, Start.AddMinutes(i * 7), 400000 + i * 60000, broken: true,
                ScoreEventClassification.Played))
            .Append(Row(chart.Id, Start.AddMinutes(45), 912400, false, ScoreEventClassification.NewPass))
            .ToArray();

        var model = await Build(chart, rows, peers: new[] { Peer("MIDNIGHT", 20.9, 930000) });

        Assert.NotNull(model.Hero);
        Assert.Equal(7, model.Hero!.Scores.Count);
        Assert.Single(model.Hero.PeerBoards);
    }

    [Fact]
    public async Task PeersSortByClosenessToMyCompetitiveLevelForTheChartsType()
    {
        // Both sides of the comparison have to be competitive levels. Sorting against the
        // chart's difficulty instead put whoever happened to sit near "21" on top.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };

        // Mine is 22.6 singles. NEAR is a fifth of a level away, FAR is nearly three.
        var model = await Build(chart, rows, peers: new[]
        {
            Peer("FAR", 19.8, 998000),
            Peer("NEAR", 22.4, 901000)
        });

        var board = Assert.Single(model.Hero!.PeerBoards);
        Assert.Equal("NEAR", board.Peers.First().PlayerName.ToString());
    }

    [Fact]
    public async Task PagingTheHistoryLeavesTheHeroExactlyWhereItWas()
    {
        // The hero is not what you paged. Rebuilding it is both wasted work and the reason the
        // interaction used to look like a navigation.
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows, Array.Empty<CommunityPeerScore>());

        var paged = await builder.Refilter(model, User, 2, 20, null, CancellationToken.None);

        Assert.Same(model.Hero, paged.Hero);
    }

    [Fact]
    public async Task PromotingACardLeavesTheHistoryExactlyWhereItWas()
    {
        var chart = ChartAt(ChartType.Single, 21);
        var rows = new[] { Row(chart.Id, Start, 912400, false, ScoreEventClassification.NewPass) };
        var (builder, model) = await BuildWith(chart, rows, Array.Empty<CommunityPeerScore>());

        var reselected = await builder.Reselect(model, User, Session, 1, 20, null, CancellationToken.None);

        Assert.Same(model.History, reselected.History);
        Assert.NotNull(reselected.Hero);
    }

    private static async Task<SessionsPageModel> Build(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows, CommunityPeerScore[] peers)
    {
        return (await BuildWith(chart, rows, peers)).Model;
    }

    private static async Task<(SessionBreakdownBuilder Builder, SessionsPageModel Model)> BuildWith(Chart chart,
        RecentSessionsPage.ScoreEventRecord[] rows, CommunityPeerScore[] peers)
    {
        var mediator = new Mock<IMediator>();
        var group = new RecentSessionsPage.SessionGroup(Session, null, MixEnum.Phoenix, "officialImport",
            rows.Min(r => r.OccurredAt), rows.Max(r => r.OccurredAt), rows);

        Setup(mediator, new GetRecentSessionsQuery(User, 1, 20),
            new RecentSessionsPage(1, new[] { group }));
        Setup(mediator, new GetScoreSessionsQuery(User), (IReadOnlyList<ScoreSessionRecord>)Array.Empty<ScoreSessionRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });
        mediator.Setup(m => m.Send(It.IsAny<GetScoreHighlightsForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ScoreHighlightRecord(chart.Id, Session, Start, HighlightFlags.FolderDebut, 21, 21.0,
                    new HighlightDetail(PeerPercentile: 0.4, AttemptsBeforeClear: 6))
            });
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerMilestonesForSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PlayerMilestoneRecord>());
        mediator.Setup(m => m.Send(It.IsAny<GetPlayerStatsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerStatsRecord(User, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1,
                22.6, 22.6, 23.4));
        mediator.Setup(m => m.Send(It.IsAny<GetCommunityPeerScoresQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyDictionary<Guid, IReadOnlyList<CommunityPeerScore>>)
                new Dictionary<Guid, IReadOnlyList<CommunityPeerScore>> { [chart.Id] = peers });

        var readers = new Mock<IUserReader>();
        readers.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
        var builder = new SessionBreakdownBuilder(mediator.Object, readers.Object);
        return (builder, await builder.Build(User, null, 1, 20, null, CancellationToken.None));
    }

    private static void Setup<T>(Mock<IMediator> mediator, IRequest<T> request, T result)
    {
        mediator.Setup(m => m.Send(It.Is<IRequest<T>>(r => r.GetType() == request.GetType()),
            It.IsAny<CancellationToken>())).ReturnsAsync(result);
    }

    private static Chart ChartAt(ChartType type, int level)
    {
        var song = new Song("Seeded Song", SongType.Arcade, new Uri("https://example.invalid/a.png"),
            TimeSpan.FromMinutes(2), "Artist", null);
        return new Chart(Guid.NewGuid(), MixEnum.Phoenix, song, type, DifficultyLevel.From(level),
            MixEnum.Phoenix, null, null, new HashSet<Skill>());
    }

    private static RecentSessionsPage.ScoreEventRecord Row(Guid chartId, DateTimeOffset at, int score,
        bool broken, ScoreEventClassification classification)
    {
        return new RecentSessionsPage.ScoreEventRecord(chartId, at, score, broken ? null : "Fair Game",
            broken, "seed", Session, classification, null);
    }

    private static CommunityPeerScore Peer(string name, double competitive, int score)
    {
        return new CommunityPeerScore(Guid.NewGuid(), Name.From(name), new[] { Name.From("Arrow Eclipse") },
            competitive, PhoenixScore.From(score), PhoenixPlate.FairGame, false);
    }
}
