using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Contracts.Queries;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The reader resolves the sources a player ticked and hands the calculator what it needs. The
///     rules worth pinning here are the ones the ports decide: whose selection is read, which
///     sources are even asked, and that the subject is never one of their own peers.
/// </summary>
public sealed class PeerStandingReaderTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Stranger = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Sealed = new(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<ICommunityReader> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRivalRepository> _rivals = new();
    private readonly Mock<IScoreReader> _scores = new();
    private readonly Mock<IPlayerStatsReader> _stats = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly Mock<IPlayerVisibilityReader> _visibility = new();
    private readonly Dictionary<string, string> _settings = new();
    private readonly Chart _single = new ChartBuilder().WithType(ChartType.Single).WithLevel(21).Build();
    private readonly Chart _coop = new ChartBuilder().WithType(ChartType.CoOp).WithLevel(2).Build();

    public PeerStandingReaderTests()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new UserBuilder().WithId(Me).Build());
        _mediator.Setup(m => m.Send(It.IsAny<GetUserUiSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _settings);
        _mediator.Setup(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _mediator.Setup(m => m.Send(It.IsAny<ResolveOfficialPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OfficialPlayerResolution>());
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(null, Array.Empty<OfficialTagScore>()));
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, DifficultyLevel? _, ChartType? _, IEnumerable<Guid>? ids, CancellationToken _) =>
                new[] { _single, _coop }.Where(c => ids!.Contains(c.Id)).ToArray());
        _rivals.Setup(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RivalEdge>());
        _communities.Setup(c => c.GetUserCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommunityOverviewRecord>());
        _stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MixEnum _, Guid id, CancellationToken _) => Stats(id, 21.3));
        _stats.Setup(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<UserPhoenixScore>());
        _scores.Setup(s => s.GetBrokenBests(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<(Guid, Guid)>());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());
    }

    private static PlayerStatsRecord Stats(Guid id, double level) =>
        new(id, 1000, 21, 100, 0, 0, 800, 900000, 21, 800, 900000, 21, 700, 880000, 20, level, level, level - 1);

    private PeerStandingReader Reader() => new(_rivals.Object,
        new RivalSubjectResolver(_users.Object, _mediator.Object),
        new RivalScoreReader(_scores.Object, _mediator.Object),
        _communities.Object, _stats.Object, _scores.Object, _charts.Object, _users.Object, _visibility.Object,
        _mediator.Object, _currentUser.Object, new MemoryCache(new MemoryCacheOptions()));

    private void MyBestIs(Guid chartId, int score) =>
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RecordedPhoenixScore(chartId, PhoenixScore.From(score), null, false, At)
            });

    private void BandIs(params Guid[] ids) =>
        _stats.Setup(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), ChartType.Single, It.IsAny<double>(),
                .5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

    private void PeerScoresAre(params UserPhoenixScore[] scores) =>
        _scores.Setup(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(scores);

    private static UserPhoenixScore Score(Guid user, Guid chart, int score) =>
        new(user, chart, "peer", PhoenixScore.From(score), null, false);

    [Fact]
    public async Task TheDefaultIsTheCompetitiveBandWithYouRemoved()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        MyBestIs(_single.Id, 950_000);
        BandIs(Me, a, b);
        PeerScoresAre(Score(a, _single.Id, 960_000), Score(b, _single.Id, 940_000));

        var standing = (await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id }))[_single.Id];

        Assert.Equal(2, standing.PeerCount);
        Assert.Equal(2, standing.Place);
        Assert.Equal(3, standing.Cohort);
        var line = Assert.Single(standing.Sources);
        Assert.Equal(PeerSourceKind.CompetitiveLevel, line.Kind);
    }

    [Fact]
    public async Task NothingTickedMeansNoStandingsAtAll()
    {
        _settings[PeerSourceSelection.SettingKey] = PeerSourceSelection.Nothing.Serialize();
        MyBestIs(_single.Id, 950_000);
        BandIs(Guid.NewGuid());

        var standings = await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id });

        Assert.Empty(standings);
        _scores.Verify(s => s.GetPlayerScores(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RivalsAndACommunityUnionWithOneLinePerSource()
    {
        var rival = Guid.NewGuid();
        var clubmate = Guid.NewGuid();
        var club = Guid.NewGuid();
        _settings[PeerSourceSelection.SettingKey] =
            new PeerSourceSelection(true, false, false, new HashSet<Guid> { club }).Serialize();
        _rivals.Setup(r => r.GetRivalsOwnedBy(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(Guid.NewGuid(), Me, rival, null, At) });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserBuilder().WithId(rival).WithName("Rival").Build() });
        _communities.Setup(c => c.GetUserCommunities(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new CommunityOverviewRecord("Club", CommunityPrivacyType.Private, 3, false, club) });
        _communities.Setup(c => c.GetMembers(It.IsAny<Name>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Me, clubmate, rival });
        MyBestIs(_single.Id, 950_000);
        PeerScoresAre(Score(rival, _single.Id, 990_000), Score(clubmate, _single.Id, 900_000));

        var standing = (await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id }))[_single.Id];

        // The rival who is also a clubmate is one peer, not two; each line reads its own group.
        Assert.Equal(2, standing.PeerCount);
        Assert.Equal(2, standing.Place);
        Assert.Equal(new[] { PeerSourceKind.Rivals, PeerSourceKind.Community },
            standing.Sources.Select(s => s.Kind));
        Assert.Equal("Club", standing.Sources[1].CommunityName);
        Assert.Equal((2, 3), (standing.Sources[1].Place, standing.Sources[1].Of));
    }

    [Fact]
    public async Task AViewerWhoIsNotTheSubjectGetsTheCompetitiveDefaultNotTheSubjectsRivals()
    {
        var rival = Guid.NewGuid();
        var bandPeer = Guid.NewGuid();
        _settings[PeerSourceSelection.SettingKey] = new PeerSourceSelection(true, false, false,
            new HashSet<Guid>()).Serialize();
        _rivals.Setup(r => r.GetRivalsOwnedBy(Stranger, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(Guid.NewGuid(), Stranger, rival, null, At) });
        MyBestIs(_single.Id, 950_000);
        BandIs(bandPeer);
        PeerScoresAre(Score(bandPeer, _single.Id, 900_000));

        var standing = (await Reader().Handle(new GetPeerStandingsQuery(MixEnum.Phoenix, new[] { _single.Id },
            Stranger), CancellationToken.None))[_single.Id];

        Assert.Equal(PeerSourceKind.CompetitiveLevel, Assert.Single(standing.Sources).Kind);
        _rivals.Verify(r => r.GetRivalsOwnedBy(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABrokenBestAmongThePeersIsCountedNotRanked()
    {
        var passer = Guid.NewGuid();
        var breaker = Guid.NewGuid();
        MyBestIs(_single.Id, 950_000);
        BandIs(passer, breaker);
        PeerScoresAre(Score(passer, _single.Id, 990_000));
        _scores.Setup(s => s.GetBrokenBests(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { (breaker, _single.Id) });

        var standing = (await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id }))[_single.Id];

        Assert.Equal(2, standing.Place);
        Assert.Equal(2, standing.Cohort);
        Assert.Equal(1, standing.Broke);
        Assert.Equal(1, standing.NotPassed);
    }

    [Fact]
    public async Task ABoardOnlyRivalCountsThroughTheMirrorOnTheChartsItPublishes()
    {
        var edge = Guid.NewGuid();
        _settings[PeerSourceSelection.SettingKey] = new PeerSourceSelection(true, false, false,
            new HashSet<Guid>()).Serialize();
        _rivals.Setup(r => r.GetRivalsOwnedBy(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(edge, Me, null, "PUMPKING#1", At) });
        _mediator.Setup(m => m.Send(It.IsAny<ResolveOfficialPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OfficialPlayerResolution("PUMPKING#1", null, null, true) });
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialScoresForTagsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialTagScores(Sealed, new[] { new OfficialTagScore("PUMPKING#1", _single.Id, 1, 998_000) }));
        MyBestIs(_single.Id, 950_000);

        var standing = (await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id }))[_single.Id];

        Assert.Equal(2, standing.Place);
        Assert.Equal(Sealed, standing.OfficialAsOf);
        Assert.Equal(1, Assert.Single(standing.Sources).FromOfficialBoard);
    }

    [Fact]
    public async Task ThePumbilityPoolIsAskedOnlyWhenTicked()
    {
        MyBestIs(_single.Id, 950_000);
        BandIs(Guid.NewGuid());

        await Reader().GetStandings(Me, MixEnum.Phoenix2, new[] { _single.Id });
        _mediator.Verify(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _settings[PeerSourceSelection.SettingKey] = new PeerSourceSelection(false, true, true,
            new HashSet<Guid>()).Serialize();
        await Reader().GetStandings(Me, MixEnum.Phoenix2, new[] { _single.Id });
        _mediator.Verify(m => m.Send(It.IsAny<GetPumbilityPeersQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ACoOpChartHasNoCompetitiveSideSoOnlyTheChosenPeopleCarryIt()
    {
        var rival = Guid.NewGuid();
        _settings[PeerSourceSelection.SettingKey] = new PeerSourceSelection(true, true, false,
            new HashSet<Guid>()).Serialize();
        _rivals.Setup(r => r.GetRivalsOwnedBy(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(Guid.NewGuid(), Me, rival, null, At) });
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserBuilder().WithId(rival).Build() });
        MyBestIs(_coop.Id, 950_000);
        PeerScoresAre(Score(rival, _coop.Id, 900_000));

        var standing = (await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _coop.Id }))[_coop.Id];

        Assert.Equal(PeerSourceKind.Rivals, Assert.Single(standing.Sources).Kind);
        _stats.Verify(s => s.GetPlayersByCompetitiveRange(It.IsAny<MixEnum>(), It.IsAny<ChartType?>(),
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AChartYouHaveNotPassedHasNoStanding()
    {
        _scores.Setup(s => s.GetBestScores(It.IsAny<MixEnum>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RecordedPhoenixScore(_single.Id, PhoenixScore.From(400_000), null, true, At) });
        BandIs(Guid.NewGuid());

        var standings = await Reader().GetStandings(Me, MixEnum.Phoenix, new[] { _single.Id });

        Assert.Empty(standings);
    }

    [Fact]
    public async Task TheRosterSortsVisiblePeersNearestYourLevelAndCarriesGhostsSeparately()
    {
        var near = Guid.NewGuid();
        var far = Guid.NewGuid();
        var hidden = Guid.NewGuid();
        var edge = Guid.NewGuid();
        _settings[PeerSourceSelection.SettingKey] = new PeerSourceSelection(true, true, false,
            new HashSet<Guid>()).Serialize();
        _rivals.Setup(r => r.GetRivalsOwnedBy(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new RivalEdge(edge, Me, null, "GHOST#1", At) });
        _mediator.Setup(m => m.Send(It.IsAny<ResolveOfficialPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OfficialPlayerResolution("GHOST#1", null, null, true) });
        BandIs(near, far, hidden);
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new UserBuilder().WithId(near).WithName("Near").Build(),
                new UserBuilder().WithId(far).WithName("Far").Build(),
                new UserBuilder().WithId(hidden).WithName("Hidden").WithIsPublic(false).Build()
            });
        _visibility.Setup(v => v.GetAudience(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerAudience(Me, new Dictionary<Guid, IReadOnlyList<Name>>(), new HashSet<Guid>()));
        _stats.Setup(s => s.GetStats(It.IsAny<MixEnum>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Stats(near, 21.4), Stats(far, 21.8) });

        var roster = await Reader().Handle(new GetMyPeerRosterQuery(MixEnum.Phoenix, ChartType.Single, 25),
            CancellationToken.None);

        Assert.Equal(new[] { "Near", "Far" }, roster.Players.Select(p => p.User.Name.ToString()));
        Assert.True(roster.Players[0].IsCompetitive);
        Assert.Equal("GHOST#1", Assert.Single(roster.BoardOnlyRivals).DisplayName);
        Assert.Equal(21.3, roster.MyLevel);
    }
}
