using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Records;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Messages;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class AccountPurgeSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IMergeRequestRepository> _merges = new();
    private readonly Mock<IAccountPurgeRepository> _purge = new();
    private readonly Mock<ConsumeContext<ProcessAccountPurgesCommand>> _context = new();
    private readonly AccountPurgeSaga _saga;

    public AccountPurgeSagaTests()
    {
        _context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        _saga = new AccountPurgeSaga(_merges.Object, _purge.Object, FakeDateTime.At(Now).Object,
            NullLogger<AccountPurgeSaga>.Instance);
    }

    [Fact]
    public async Task FirstFirePublishesPurgeEventDeletesIdentityDataAndKeepsTheUserRow()
    {
        var merge = MergeDueSince(Now.AddDays(-1), MergeState.Active);
        _merges.Setup(m => m.GetPurgeable(Now, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { merge });

        await _saga.Consume(_context.Object);

        _context.Verify(c => c.Publish(It.Is<AccountPurgeStartedEvent>(e =>
            e.RetiredUserId == merge.RetiredUserId), It.IsAny<CancellationToken>()), Times.Once);
        _purge.Verify(p => p.DeleteIdentityData(merge.RetiredUserId, It.IsAny<CancellationToken>()), Times.Once);
        _purge.Verify(p => p.DeleteUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _merges.Verify(m => m.Save(It.Is<MergeRequest>(saved => saved.State == MergeState.Purging),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AWeekOfRefiresLaterTheUserRowFallsAndTheMergeIsPurged()
    {
        var merge = MergeDueSince(Now.AddDays(-8), MergeState.Purging);
        _merges.Setup(m => m.GetPurgeable(Now, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { merge });

        await _saga.Consume(_context.Object);

        _context.Verify(c => c.Publish(It.IsAny<AccountPurgeStartedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _purge.Verify(p => p.DeleteUser(merge.RetiredUserId, It.IsAny<CancellationToken>()), Times.Once);
        _merges.Verify(m => m.Save(It.Is<MergeRequest>(saved =>
                saved.State == MergeState.Purged && saved.PurgedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static MergeRequest MergeDueSince(DateTimeOffset purgeAfter, MergeState state)
    {
        return new MergeRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Array.Empty<ExternalLoginRecord>(), new RetiredUserSnapshot(true, null), state,
            purgeAfter.AddDays(-30), purgeAfter, state == MergeState.Purging ? purgeAfter : null);
    }
}
