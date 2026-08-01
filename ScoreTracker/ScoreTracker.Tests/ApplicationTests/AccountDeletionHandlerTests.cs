using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Application;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Commands;
using ScoreTracker.Identity.Domain;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class AccountDeletionHandlerTests
{
    // The one account User.IsAdmin recognises.
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ICommunityReader> _communities = new();
    private readonly Mock<IAccountDeletionRepository> _deletions = new();
    private readonly Mock<IUserRepository> _users = new();

    private AccountDeletionHandlers Build(User user)
    {
        _users.Setup(u => u.GetUser(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _communities.Setup(c => c.GetOwnedCommunities(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<OwnedCommunityRecord>());
        return new AccountDeletionHandlers(_deletions.Object, _users.Object, _communities.Object,
            FakeDateTime.At(Now).Object);
    }

    [Fact]
    public async Task TheAdminAccountCannotDeleteItself()
    {
        // It administers the site and owns the World community. No self-serve flow should be
        // able to take either away, so the handler refuses even though the page hides the button.
        var handler = Build(new UserBuilder().WithId(AdminId).Build());

        var result = await handler.Handle(new RequestAccountDeletionCommand(AdminId), CancellationToken.None);

        Assert.Equal(AccountDeletionOutcome.NotPermitted, result.Outcome);
        _deletions.Verify(d => d.Save(It.IsAny<AccountDeletionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _users.Verify(u => u.SaveUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OwningACommunityRefusesAndSaysWhich()
    {
        var userId = Guid.NewGuid();
        var handler = Build(new UserBuilder().WithId(userId).Build());
        _communities.Setup(c => c.GetOwnedCommunities(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new OwnedCommunityRecord(Name.From("Pump United"), 90, 0) });

        var result = await handler.Handle(new RequestAccountDeletionCommand(userId), CancellationToken.None);

        Assert.Equal(AccountDeletionOutcome.BlockedByOwnedCommunities, result.Outcome);
        Assert.Equal("Pump United", result.Blockers.Single().CommunityName.ToString());
        _deletions.Verify(d => d.Save(It.IsAny<AccountDeletionRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SchedulingHidesTheAccountAndSnapshotsWhatToRestore()
    {
        var userId = Guid.NewGuid();
        var user = new UserBuilder().WithId(userId).Build() with
        {
            IsPublic = true, GameTag = Name.From("SHIRONEKO")
        };
        var handler = Build(user);

        var result = await handler.Handle(new RequestAccountDeletionCommand(userId), CancellationToken.None);

        Assert.Equal(AccountDeletionOutcome.Scheduled, result.Outcome);
        _deletions.Verify(d => d.Save(It.Is<AccountDeletionRequest>(r =>
            r.UserId == userId && r.WasPublic && r.GameTag == "SHIRONEKO" &&
            r.PurgeAfter == Now.AddDays(7)), It.IsAny<CancellationToken>()), Times.Once);
        // Hidden right away, so nobody meets a ghost during the window.
        _users.Verify(u => u.SaveUser(It.Is<User>(saved => !saved.IsPublic && saved.GameTag == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellingRestoresTheSnapshot()
    {
        var userId = Guid.NewGuid();
        var handler = Build(new UserBuilder().WithId(userId).Build());
        _deletions.Setup(d => d.GetPending(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccountDeletionRequest(Guid.NewGuid(), userId, Now.AddDays(-1),
                Now.AddDays(6), null, null, true, "SHIRONEKO"));

        await handler.Handle(new CancelAccountDeletionCommand(userId), CancellationToken.None);

        _users.Verify(u => u.SaveUser(It.Is<User>(saved => saved.IsPublic && saved.GameTag == "SHIRONEKO"),
            It.IsAny<CancellationToken>()), Times.Once);
        _deletions.Verify(d => d.Save(It.Is<AccountDeletionRequest>(r => r.CancelledAt == Now),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
