using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestHelpers;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>Keys, invites and the grants that decide who may read whom.</summary>
public sealed class ToolKeyAndShareHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ToolId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid MakerId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private readonly Mock<IToolKeyRepository> _keys = new();
    private readonly Mock<IToolRepository> _tools = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public ToolKeyAndShareHandlerTests()
    {
        _currentUser.SetupGet(c => c.User).Returns(new ScoreTracker.Domain.Models.User(
            MakerId, Name.From("DrMurloc"), true, null, new Uri("https://example.com/a.png"), null));
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tool.Create(ToolId, MakerId, Name.From("Planner"), Now));
    }

    private ToolKeySaga KeySaga()
    {
        return new ToolKeySaga(_keys.Object, _tools.Object, _users.Object, _currentUser.Object,
            FakeDateTime.At(Now).Object);
    }

    [Fact]
    public async Task AMintedKeyIsReturnedOnceAndStoredHashed()
    {
        _keys.Setup(k => k.GetKeys(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ToolApiKeyRecord>());
        string? storedHash = null;
        _keys.Setup(k => k.AddKey(ToolId, It.IsAny<Guid>(), "production", It.IsAny<string>(),
                It.IsAny<string>(), Now, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Callback((Guid _, Guid _, string _, string hash, string _, DateTimeOffset _,
                DateTimeOffset? _, CancellationToken _) => storedHash = hash)
            .Returns(Task.CompletedTask);

        var minted = await KeySaga().Handle(
            new CreateToolApiKeyCommand(ToolId, "production", Now.AddDays(182)), CancellationToken.None);

        Assert.StartsWith(ApiKeyMint.Prefix, minted.Key);
        Assert.NotNull(storedHash);
        Assert.DoesNotContain(minted.Key, storedHash);
        Assert.Equal(ApiKeyMint.HashOf(minted.Key), storedHash);
    }

    // Two live keys is the rotation allowance; a third would just be sprawl.
    [Fact]
    public async Task AThirdLiveKeyIsRefused()
    {
        _keys.Setup(k => k.GetKeys(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ToolApiKeyRecord(Guid.NewGuid(), "a", "1111", Now, null, null, null),
                new ToolApiKeyRecord(Guid.NewGuid(), "b", "2222", Now, Now.AddDays(30), null, null)
            });

        await Assert.ThrowsAsync<ToolListingException>(() => KeySaga()
            .Handle(new CreateToolApiKeyCommand(ToolId, "c", null), CancellationToken.None));
    }

    // A revoked or expired key does not count against the allowance, or rotation would deadlock.
    [Fact]
    public async Task RevokedAndExpiredKeysDoNotBlockANewOne()
    {
        _keys.Setup(k => k.GetKeys(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ToolApiKeyRecord(Guid.NewGuid(), "revoked", "1111", Now, null, null, Now),
                new ToolApiKeyRecord(Guid.NewGuid(), "expired", "2222", Now, Now.AddDays(-1), null, null)
            });

        var minted = await KeySaga().Handle(new CreateToolApiKeyCommand(ToolId, "fresh", null),
            CancellationToken.None);

        Assert.NotNull(minted.Key);
    }

    [Fact]
    public async Task AMalformedBearerTokenNeverReachesTheDatabase()
    {
        var resolved = await KeySaga().Handle(new GetToolByApiKeyQuery("not-a-key"), CancellationToken.None);

        Assert.Null(resolved);
        _keys.Verify(k => k.ResolveToolByKeyHash(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AWellFormedKeyIsLookedUpByItsHashNotItsText()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolId);

        var resolved = await KeySaga().Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);

        Assert.Equal(ToolId, resolved);
    }

    [Fact]
    public async Task AnotherMakersToolIsIndistinguishableFromOneThatDoesNotExist()
    {
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tool.Create(ToolId, Guid.NewGuid(), Name.From("Someone else's"), Now));

        await Assert.ThrowsAsync<ToolNotFoundException>(() => KeySaga()
            .Handle(new CreateToolInviteLinkCommand(ToolId), CancellationToken.None));
    }

    private ToolAccessSaga AccessSaga()
    {
        return new ToolAccessSaga(_tools.Object, _users.Object, _currentUser.Object, FakeDateTime.At(Now).Object);
    }

    // A player saying "not this one" means it whichever route the tool arrived by, so the block
    // drops any direct grant too.
    [Fact]
    public async Task BlockingATooAlsoRevokesADirectGrant()
    {
        await AccessSaga().Handle(new BlockToolCommand(ToolId), CancellationToken.None);

        _tools.Verify(t => t.RevokeShare(ToolId, MakerId, Now, It.IsAny<CancellationToken>()), Times.Once);
        _tools.Verify(t => t.BlockTool(ToolId, MakerId, Now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConnectingGrantsADirectShare()
    {
        await AccessSaga().Handle(new ConnectToolCommand(ToolId), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(ToolId, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // Without this a maker has no real account to test against, and finding themselves in their
    // own directory would be a silly first step.
    [Fact]
    public async Task CreatingAToolConnectsItsMakerAsPlayerOne()
    {
        var saga = new ToolManagementSaga(_tools.Object, _currentUser.Object, FakeDateTime.At(Now).Object);

        var id = await saga.Handle(new CreateToolCommand("Planner"), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(id, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
