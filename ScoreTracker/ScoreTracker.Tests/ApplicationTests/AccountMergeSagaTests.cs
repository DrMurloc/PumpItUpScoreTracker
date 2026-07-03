using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Moq;
using ScoreTracker.Domain.Exceptions;
using ScoreTracker.Domain.Models;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Contracts.Events;
using ScoreTracker.Identity.Domain;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class AccountMergeSagaTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly User _survivor = new UserBuilder().WithGameTag("KEEPER").Build();
    private readonly User _retired = new UserBuilder().WithGameTag("SPOOKZ").WithIsPublic(true).Build();

    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IMergeRequestRepository> _merges = new();
    private readonly Mock<IBus> _bus = new();
    private readonly AccountMergeSaga _saga;

    public AccountMergeSagaTests()
    {
        _currentUser.Setup(c => c.User).Returns(_survivor);
        _users.Setup(u => u.GetUser(_survivor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_survivor);
        _users.Setup(u => u.GetUser(_retired.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_retired);
        _merges.Setup(m => m.GetActiveInvolving(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MergeRequest>());
        _users.Setup(u => u.GetExternalLogins(_retired.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ExternalLoginRecord("PiuGame", "mbid:spookz") });
        _saga = new AccountMergeSaga(_currentUser.Object, _users.Object, _merges.Object, _bus.Object,
            FakeDateTime.At(Now).Object);
    }

    [Fact]
    public async Task MergeMovesLoginsHidesRetiredRecordsRequestAndPublishesEvent()
    {
        var result = await _saga.Handle(new ExecuteAccountMergeCommand(_survivor.Id, _retired.Id),
            CancellationToken.None);

        Assert.Equal(_survivor.Id, result.SurvivorUserId);
        Assert.Equal(Now.AddDays(30), result.PurgeAfter);

        _users.Verify(u => u.RemoveExternalLogin(_retired.Id, "PiuGame", "mbid:spookz",
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.CreateExternalLogin(_survivor.Id, "PiuGame", "mbid:spookz",
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SaveUser(It.Is<User>(saved =>
                saved.Id == _retired.Id && !saved.IsPublic && saved.GameTag == null &&
                saved.ClaimsInvalidatedAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
        _merges.Verify(m => m.Save(It.Is<MergeRequest>(merge =>
                merge.SurvivorUserId == _survivor.Id && merge.RetiredUserId == _retired.Id &&
                merge.State == MergeState.Active && merge.PurgeAfter == Now.AddDays(30) &&
                merge.MovedLogins.Single().ExternalId == "mbid:spookz" &&
                merge.Snapshot.IsPublic && merge.Snapshot.GameTag == "SPOOKZ"),
            It.IsAny<CancellationToken>()), Times.Once);
        _bus.Verify(b => b.Publish(It.Is<AccountsMergedEvent>(e =>
                e.SurvivorUserId == _survivor.Id && e.RetiredUserId == _retired.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MergeRequiresTheCurrentUserToBeAParticipant()
    {
        var outsider = new UserBuilder().Build();
        _currentUser.Setup(c => c.User).Returns(outsider);

        await Assert.ThrowsAsync<InvalidAccountMergeException>(() =>
            _saga.Handle(new ExecuteAccountMergeCommand(_survivor.Id, _retired.Id), CancellationToken.None));
    }

    [Fact]
    public async Task MergeRejectsSelfMerge()
    {
        await Assert.ThrowsAsync<InvalidAccountMergeException>(() =>
            _saga.Handle(new ExecuteAccountMergeCommand(_survivor.Id, _survivor.Id), CancellationToken.None));
    }

    [Fact]
    public async Task MergeRejectsAccountsAlreadyInsideAGraceWindow()
    {
        _merges.Setup(m => m.GetActiveInvolving(_retired.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ExistingMerge() });

        await Assert.ThrowsAsync<InvalidAccountMergeException>(() =>
            _saga.Handle(new ExecuteAccountMergeCommand(_survivor.Id, _retired.Id), CancellationToken.None));
    }

    [Fact]
    public async Task UndoRestoresLoginsAndVisibility()
    {
        var merge = ExistingMerge();
        _merges.Setup(m => m.Get(merge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(merge);
        _users.Setup(u => u.GetUser(_retired.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_retired with { IsPublic = false, GameTag = null });

        await _saga.Handle(new UndoAccountMergeCommand(merge.Id), CancellationToken.None);

        _users.Verify(u => u.RemoveExternalLogin(_survivor.Id, "PiuGame", "mbid:spookz",
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.CreateExternalLogin(_retired.Id, "PiuGame", "mbid:spookz",
            It.IsAny<CancellationToken>()), Times.Once);
        _users.Verify(u => u.SaveUser(It.Is<User>(saved =>
                saved.Id == _retired.Id && saved.IsPublic && saved.GameTag != null &&
                saved.GameTag.ToString() == "SPOOKZ"),
            It.IsAny<CancellationToken>()), Times.Once);
        _merges.Verify(m => m.Save(It.Is<MergeRequest>(saved => saved.State == MergeState.Undone),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UndoIsSurvivorOnly()
    {
        var merge = ExistingMerge();
        _merges.Setup(m => m.Get(merge.Id, It.IsAny<CancellationToken>())).ReturnsAsync(merge);
        _currentUser.Setup(c => c.User).Returns(new UserBuilder().Build());

        await Assert.ThrowsAsync<InvalidAccountMergeException>(() =>
            _saga.Handle(new UndoAccountMergeCommand(merge.Id), CancellationToken.None));
    }

    private MergeRequest ExistingMerge()
    {
        return new MergeRequest(Guid.NewGuid(), _survivor.Id, _retired.Id,
            new[] { new ExternalLoginRecord("PiuGame", "mbid:spookz") },
            new RetiredUserSnapshot(true, "SPOOKZ"), MergeState.Active, Now, Now.AddDays(30), null);
    }
}
