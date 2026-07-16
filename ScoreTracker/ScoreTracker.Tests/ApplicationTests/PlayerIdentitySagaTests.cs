using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerIdentitySagaTests
{
    private static readonly RenameProposal Pending =
        new(4, OldPlayerId: 11, NewPlayerId: 22, "OLDTAG", "NEWTAG", true, 46, ProposalStatuses.Pending, 3);

    private static (Mock<IOfficialPlayerIdentityRepository> Identity, PlayerIdentitySaga Saga) Arrange(
        RenameProposal? proposal = null)
    {
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        identity.Setup(i => i.GetProposal(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);
        return (identity, new PlayerIdentitySaga(identity.Object, NullLogger<PlayerIdentitySaga>.Instance));
    }

    [Fact]
    public async Task AcceptMergesTheOldPlayerIntoTheNewAndMarksAccepted()
    {
        var (identity, saga) = Arrange(Pending);

        await saga.Handle(new AcceptRenameProposalCommand(Pending.Id), CancellationToken.None);

        identity.Verify(i => i.MergePlayers(11, 22, It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(i => i.SetProposalStatus(Pending.Id, ProposalStatuses.Accepted,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptOnANonPendingProposalMergesNothing()
    {
        var (identity, saga) = Arrange(Pending with { Status = ProposalStatuses.Dismissed });

        await saga.Handle(new AcceptRenameProposalCommand(Pending.Id), CancellationToken.None);

        identity.Verify(i => i.MergePlayers(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        identity.Verify(i => i.SetProposalStatus(It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DismissOnlyChangesTheStatus()
    {
        var (identity, saga) = Arrange(Pending);

        await saga.Handle(new DismissRenameProposalCommand(Pending.Id), CancellationToken.None);

        identity.Verify(i => i.SetProposalStatus(Pending.Id, ProposalStatuses.Dismissed,
            It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(i => i.MergePlayers(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PendingProposalsProjectToTheAdminRecordShape()
    {
        var (identity, saga) = Arrange();
        identity.Setup(i => i.GetProposals(MixEnum.Phoenix2, ProposalStatuses.Pending,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { Pending });

        var records = await saga.Handle(new GetRenameProposalsQuery(MixEnum.Phoenix2), CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(Pending.Id, record.Id);
        Assert.Equal("OLDTAG", record.OldUsername);
        Assert.Equal("NEWTAG", record.NewUsername);
        Assert.Equal(46, record.Top50Overlap);
    }

    [Fact]
    public async Task AccountMergesRelinkMirrorPlayersToTheSurvivor()
    {
        var (identity, saga) = Arrange();
        var retired = Guid.NewGuid();
        var survivor = Guid.NewGuid();
        var context = new Mock<ConsumeContext<AccountsMergedEvent>>();
        context.SetupGet(c => c.Message).Returns(new AccountsMergedEvent(survivor, retired));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);

        await saga.Consume(context.Object);

        identity.Verify(i => i.RelinkUser(retired, survivor, It.IsAny<CancellationToken>()), Times.Once);
    }
}
