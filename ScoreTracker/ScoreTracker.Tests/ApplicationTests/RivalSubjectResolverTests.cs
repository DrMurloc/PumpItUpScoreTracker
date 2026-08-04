using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.Rivals.Application;
using ScoreTracker.Rivals.Contracts;
using ScoreTracker.Rivals.Domain;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class RivalSubjectResolverTests
{
    private static readonly DateTimeOffset Added = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _users = new();

    private RivalSubjectResolver Resolver() => new(_users.Object, _mediator.Object);

    private void UsersAre(params User[] users) =>
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

    private void GhostsAre(params OfficialPlayerResolution[] ghosts) =>
        _mediator.Setup(m => m.Send(It.IsAny<ResolveOfficialPlayersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ghosts);

    private static RivalEdge SiteEdge(Guid userId) => new(Guid.NewGuid(), Guid.NewGuid(), userId, null, Added);
    private static RivalEdge TagEdge(string tag) => new(Guid.NewGuid(), Guid.NewGuid(), null, tag, Added);

    [Fact]
    public async Task ASiteRivalAnswersForScoresFoldersAndProgression()
    {
        var target = Guid.NewGuid();
        UsersAre(new UserBuilder().WithId(target).WithName("ERRLENA").Build());
        GhostsAre();

        var subject = Assert.Single(
            await Resolver().Resolve(new[] { SiteEdge(target) }, MixEnum.Phoenix, CancellationToken.None));

        Assert.False(subject.IsGhost);
        Assert.Equal("ERRLENA", subject.DisplayName);
        Assert.True(subject.Can(RivalCapabilities.LiveScores));
        Assert.True(subject.Can(RivalCapabilities.FolderCompare));
        Assert.True(subject.Can(RivalCapabilities.Progression));
    }

    /// <summary>
    ///     The whole point of the capability set: a board-only rival is short by construction, and
    ///     a component handed this subject renders standings and nothing it cannot fill.
    /// </summary>
    [Fact]
    public async Task AGhostRivalAnswersForStandingsOnly()
    {
        UsersAre();
        GhostsAre(new OfficialPlayerResolution("FRANKEZA#9606", null, new Uri("https://piu.test/f.png"), true));

        var subject = Assert.Single(
            await Resolver().Resolve(new[] { TagEdge("FRANKEZA#9606") }, MixEnum.Phoenix,
                CancellationToken.None));

        Assert.True(subject.IsGhost);
        Assert.True(subject.Can(RivalCapabilities.OfficialStandings));
        Assert.False(subject.Can(RivalCapabilities.LiveScores));
        Assert.False(subject.Can(RivalCapabilities.FolderCompare));
        Assert.True(subject.IsOnCurrentBoards);
    }

    /// <summary>
    ///     A tag that linked between the add and this read. The promote consumer will rewrite the
    ///     row, but the render must not wait for it.
    /// </summary>
    [Fact]
    public async Task ATagThatHasLinkedResolvesAsTheAccountBeforeTheRowIsRewritten()
    {
        var linked = Guid.NewGuid();
        UsersAre();
        GhostsAre(new OfficialPlayerResolution("KAZE#4366", linked, null, true));

        var subject = Assert.Single(
            await Resolver().Resolve(new[] { TagEdge("KAZE#4366") }, MixEnum.Phoenix, CancellationToken.None));

        Assert.False(subject.IsGhost);
        Assert.Equal(linked, subject.UserId);
        Assert.True(subject.Can(RivalCapabilities.LiveScores));
        Assert.True(subject.Can(RivalCapabilities.OfficialStandings));
    }

    /// <summary>
    ///     An undetected rename leaves a tag the boards no longer carry. The row still renders —
    ///     it is somebody the user deliberately chose, and a row they can act on beats one that
    ///     silently vanished.
    /// </summary>
    [Fact]
    public async Task AnUnresolvableTagStillRendersWithNoCapabilities()
    {
        UsersAre();
        GhostsAre();

        var subject = Assert.Single(
            await Resolver().Resolve(new[] { TagEdge("GONE#0001") }, MixEnum.Phoenix, CancellationToken.None));

        Assert.Equal("GONE#0001", subject.DisplayName);
        Assert.Equal(RivalCapabilities.None, subject.Capabilities);
        Assert.False(subject.IsOnCurrentBoards);
    }

    /// <summary>A user deleted between the add and this read has nobody left to name.</summary>
    [Fact]
    public async Task ASiteRivalWhoseAccountIsGoneDrops()
    {
        UsersAre();
        GhostsAre();

        var subjects = await Resolver().Resolve(new[] { SiteEdge(Guid.NewGuid()) }, MixEnum.Phoenix,
            CancellationToken.None);

        Assert.Empty(subjects);
    }

    /// <summary>
    ///     Three hundred edges is the shape this will be exercised in, so both sides resolve in one
    ///     round trip each rather than one per rival.
    /// </summary>
    [Fact]
    public async Task ResolvesEveryEdgeInOneRoundTripPerSide()
    {
        var users = Enumerable.Range(0, 50).Select(_ => new UserBuilder().WithId(Guid.NewGuid()).Build())
            .ToArray();
        var tags = Enumerable.Range(0, 50).Select(i => $"TAG{i}").ToArray();
        UsersAre(users);
        GhostsAre(tags.Select(t => new OfficialPlayerResolution(t, null, null, true)).ToArray());
        var edges = users.Select(u => SiteEdge(u.Id)).Concat(tags.Select(TagEdge)).ToArray();

        var subjects = await Resolver().Resolve(edges, MixEnum.Phoenix, CancellationToken.None);

        Assert.Equal(100, subjects.Count);
        _users.Verify(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mediator.Verify(m => m.Send(It.IsAny<ResolveOfficialPlayersQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LinkPromotesEveryEdgeHoldingTheTag()
    {
        var rivals = new Mock<IRivalRepository>();
        var userId = Guid.NewGuid();
        rivals.Setup(r => r.PromoteTagToUser("KAZE#4366", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        var saga = new OfficialPlayerLinkSaga(rivals.Object, NullLogger<OfficialPlayerLinkSaga>.Instance);

        await saga.Consume(Context(new OfficialPlayerLinkedEvent(MixEnum.Phoenix, "KAZE#4366", userId)));

        rivals.Verify(r => r.PromoteTagToUser("KAZE#4366", userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RenameRewritesEveryEdgeHoldingTheOldTag()
    {
        var rivals = new Mock<IRivalRepository>();
        var saga = new OfficialPlayerRenameSaga(rivals.Object, NullLogger<OfficialPlayerRenameSaga>.Instance);

        await saga.Consume(Context(new OfficialPlayerRenamedEvent(MixEnum.Phoenix, "OLD#1", "NEW#2")));

        rivals.Verify(r => r.RenameTag("OLD#1", "NEW#2", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }
}
