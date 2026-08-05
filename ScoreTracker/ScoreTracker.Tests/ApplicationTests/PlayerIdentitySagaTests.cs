using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts.Commands;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.OfficialMirror.Domain;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class PlayerIdentitySagaTests
{
    private static readonly RenameEvidence Evidence = new(50, 48, 46, 2, 1, 0, true);

    private static readonly RenameProposal Pending =
        new(4, OldPlayerId: 11, NewPlayerId: 22, "OLDTAG", "NEWTAG", VanishVerdicts.Merge, Evidence,
            ProposalStatuses.Pending, 3, MixEnum.Phoenix);

    private readonly Mock<IBus> _bus = new();

    private (Mock<IOfficialPlayerIdentityRepository> Identity, PlayerIdentitySaga Saga) Arrange(
        RenameProposal? proposal = null)
    {
        var identity = new Mock<IOfficialPlayerIdentityRepository>();
        identity.Setup(i => i.GetProposal(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);
        return (identity,
            new PlayerIdentitySaga(identity.Object, NullLogger<PlayerIdentitySaga>.Instance, _bus.Object));
    }

    [Fact]
    public async Task AcceptMergesTheOldPlayerIntoTheNewAndMarksAccepted()
    {
        var (identity, saga) = Arrange(Pending);

        await saga.Handle(new AcceptRenameProposalCommand(Pending.Id), CancellationToken.None);

        identity.Verify(i => i.MergePlayers(11, 22, It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(i => i.SetProposalStatus(Pending.Id, ProposalStatuses.Accepted,
            It.IsAny<CancellationToken>()), Times.Once);
        // The merge deletes the old dimension row, so anything holding the old tag has to hear
        // about it — a rival edge pointing at OLDTAG would otherwise dangle silently.
        _bus.Verify(b => b.Publish(It.Is<OfficialPlayerRenamedEvent>(e =>
                e.Mix == MixEnum.Phoenix && e.OldTag == "OLDTAG" && e.NewTag == "NEWTAG"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DismissAnnouncesNoRename()
    {
        var (_, saga) = Arrange(Pending);

        await saga.Handle(new DismissRenameProposalCommand(Pending.Id), CancellationToken.None);

        _bus.Verify(b => b.Publish(It.IsAny<OfficialPlayerRenamedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task AnUnattendedMergeIsRecordedAsOne()
    {
        // Same merge, different signature in the audit trail. The desk has to be able to say
        // which of these a human actually looked at, because none of them can be undone.
        var (identity, saga) = Arrange(Pending);

        await saga.Handle(new AcceptRenameProposalCommand(Pending.Id, true), CancellationToken.None);

        identity.Verify(i => i.MergePlayers(11, 22, It.IsAny<CancellationToken>()), Times.Once);
        identity.Verify(i => i.SetProposalStatus(Pending.Id, ProposalStatuses.AutoAccepted,
            It.IsAny<CancellationToken>()), Times.Once);
        _bus.Verify(b => b.Publish(It.IsAny<OfficialPlayerRenamedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AFindingWithNoCandidateMergesNothing()
    {
        // A tag that simply dropped off the boards is recorded, never merged — there is
        // nothing to merge it into.
        var (identity, saga) = Arrange(Pending with { NewPlayerId = null, NewUsername = null });

        await saga.Handle(new AcceptRenameProposalCommand(Pending.Id), CancellationToken.None);

        identity.Verify(i => i.MergePlayers(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _bus.Verify(b => b.Publish(It.IsAny<OfficialPlayerRenamedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FindingsProjectToTheAdminRecordShape()
    {
        var (identity, saga) = Arrange();
        identity.Setup(i => i.GetFindings(MixEnum.Phoenix2, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Pending });

        var records = await saga.Handle(new GetRenameProposalsQuery(MixEnum.Phoenix2), CancellationToken.None);

        var record = Assert.Single(records);
        Assert.Equal(Pending.Id, record.Id);
        Assert.Equal("OLDTAG", record.OldUsername);
        Assert.Equal("NEWTAG", record.NewUsername);
        Assert.Equal(VanishVerdicts.Merge, record.Verdict);
        Assert.Equal(46, record.ExactNonPgMatches);
        Assert.Equal(1, record.RunnerUpExactMatches);
    }

    [Fact]
    public async Task TheDeskCanAskForTheWholePopulation()
    {
        // Not just what needs deciding: a rule that has quietly stopped detecting renames
        // is indistinguishable from a quiet week unless the merges are visible too.
        var (identity, saga) = Arrange();
        identity.Setup(i => i.GetFindings(MixEnum.Phoenix2, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Pending with { Status = ProposalStatuses.AutoAccepted } });

        var records = await saga.Handle(new GetRenameProposalsQuery(MixEnum.Phoenix2),
            CancellationToken.None);

        Assert.Equal(ProposalStatuses.AutoAccepted, Assert.Single(records).Status);
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
