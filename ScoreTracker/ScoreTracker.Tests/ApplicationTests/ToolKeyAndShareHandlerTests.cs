using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Contracts.Commands;
using ScoreTracker.CommunityTools.Contracts.Queries;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.CommunityTools.Wiring;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts;
using ScoreTracker.Identity.Contracts.Queries;
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

    /// <summary>
    ///     User.IsAdmin is computed from this id rather than stored, so an admin test has to be
    ///     this person — a bool on the constructor would be IsPublic, which is a different thing.
    /// </summary>
    private static readonly Guid AdminId = Guid.Parse("E38954C4-B1B1-418A-93F6-C4B25C98B713");

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IToolSecretReader> _secrets = new();
    private readonly Mock<IToolMakerBanRepository> _bans = new();
    private readonly Mock<IRepositoryReachabilityClient> _repositories = new();
    private readonly Mock<IWebhookDeliveryClient> _webhooks = new();
    private readonly Mock<IToolKeyRepository> _keys = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly Mock<IToolRepository> _tools = new();
    private readonly Mock<IUserReader> _users = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();

    public ToolKeyAndShareHandlerTests()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(true);
        _currentUser.SetupGet(c => c.User).Returns(new ScoreTracker.Domain.Models.User(
            MakerId, Name.From("DrMurloc"), true, null, new Uri("https://example.com/a.png"), null));
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tool.Create(ToolId, MakerId, Name.From("Planner"), Now));
    }

    private ToolKeySaga KeySaga()
    {
        return new ToolKeySaga(_keys.Object, _tools.Object, _users.Object, _currentUser.Object,
            FakeDateTime.At(Now).Object, _activity.Object, _cache, NullLogger<ToolKeySaga>.Instance);
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
        _activity.Verify(a => a.Increment(It.IsAny<Guid>(), It.IsAny<ToolActivityKind>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AWellFormedKeyIsLookedUpByItsHashNotItsText()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: false));

        var resolved = await KeySaga().Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);

        Assert.Equal(new ToolKeyPrincipal(ToolId, "production"), resolved);
    }

    /// <summary>Authenticated is used: the hour's tally moves under the key's own name.</summary>
    [Fact]
    public async Task ALiveKeyAddsOneToTheHoursTallyUnderItsName()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: false));

        await KeySaga().Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);

        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.KeyUsed, Now, "production",
            It.IsAny<CancellationToken>()), Times.Once);
        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.KeyExpired, It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     The limiter runs before the scheme, so a rejected request arrives as a bare credential.
    ///     A key that resolved recently is still in the cache, and that is the name it counts under.
    /// </summary>
    [Fact]
    public async Task ARateLimitedRequestCountsUnderTheKeyThatResolvedRecently()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: false));
        var saga = KeySaga();
        await saga.Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);

        var named = await saga.Handle(new RecordRateLimitedRequestCommand(key), CancellationToken.None);

        Assert.Equal(new ToolKeyPrincipal(ToolId, "production"), named);
        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.RateLimited, Now, "production",
            It.IsAny<CancellationToken>()), Times.Once);
        // Never the database: a lookup per rejected request is the load a limit exists to refuse.
        _keys.Verify(k => k.ResolveToolByKeyHash(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     Revocation exists for a tool that is out of control, which is exactly when the limiter
    ///     is rejecting it. The first request the scheme sees after the revoke must take the key
    ///     out of the cache, or the flood keeps counting under a dead name.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AKeyThatStopsResolvingStopsCountingRateLimitsAtOnce(bool expiredRatherThanRevoked)
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: false));
        var saga = KeySaga();
        await saga.Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredRatherThanRevoked ? new ToolKeyResolution(ToolId, "production", IsExpired: true) : null);
        await saga.Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);

        var named = await saga.Handle(new RecordRateLimitedRequestCommand(key), CancellationToken.None);

        Assert.Null(named);
        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.RateLimited, It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>A key nobody resolved recently, or a personal token, is dropped rather than looked up.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ARateLimitedRequestOnAnUnseenCredentialCountsNothing(bool looksLikeAKey)
    {
        var credential = looksLikeAKey ? ApiKeyMint.Mint().Key : Guid.NewGuid().ToString();

        var named = await KeySaga().Handle(new RecordRateLimitedRequestCommand(credential), CancellationToken.None);

        Assert.Null(named);
        _activity.Verify(a => a.Increment(It.IsAny<Guid>(), It.IsAny<ToolActivityKind>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        _keys.Verify(k => k.ResolveToolByKeyHash(It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     The tally is written after the key has already authenticated. A database that refuses
    ///     the row must not turn a working call into a 500, on either path.
    /// </summary>
    [Fact]
    public async Task ATallyThatCannotBeWrittenNeverFailsTheCall()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: false));
        _activity.Setup(a => a.Increment(It.IsAny<Guid>(), It.IsAny<ToolActivityKind>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("deadlock victim"));
        var saga = KeySaga();

        var resolved = await saga.Handle(new GetToolByApiKeyQuery(key), CancellationToken.None);
        var rejected = await saga.Handle(new RecordRateLimitedRequestCommand(key), CancellationToken.None);

        Assert.Equal(new ToolKeyPrincipal(ToolId, "production"), resolved);
        Assert.Equal(new ToolKeyPrincipal(ToolId, "production"), rejected);
    }

    /// <summary>An unknown or revoked key is nobody's: nothing to count it under.</summary>
    [Fact]
    public async Task AnUnknownKeyCountsNothing()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ToolKeyResolution?)null);

        Assert.Null(await KeySaga().Handle(new GetToolByApiKeyQuery(key), CancellationToken.None));
        _activity.Verify(a => a.Increment(It.IsAny<Guid>(), It.IsAny<ToolActivityKind>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>The repository names an expired key so the console can; the caller still gets nothing.</summary>
    [Fact]
    public async Task AnExpiredKeyResolvesToNothing()
    {
        var (key, hash, _) = ApiKeyMint.Mint();
        _keys.Setup(k => k.ResolveToolByKeyHash(hash, Now, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolKeyResolution(ToolId, "production", IsExpired: true));

        Assert.Null(await KeySaga().Handle(new GetToolByApiKeyQuery(key), CancellationToken.None));
        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.KeyExpired, Now, "production",
            It.IsAny<CancellationToken>()), Times.Once);
        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.KeyUsed, It.IsAny<DateTimeOffset>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
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

    // A maker may enter session mode the moment their last player disconnects — including while
    // someone has the ordinary connect dialog open. That player agreed to score reads; granting
    // would hand over a piugame.com session instead.
    [Fact]
    public async Task ConnectingToATooThatTurnedSessionModeOnMidDialogIsRefused()
    {
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionModeTool());

        await Assert.ThrowsAsync<ToolConsentMismatchException>(() => AccessSaga()
            .Handle(new ConnectToolCommand(ToolId), CancellationToken.None));

        _tools.Verify(t => t.GrantShare(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<ShareSource>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConnectingToASessionModeToolWithTheWarningAcknowledgedGrants()
    {
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SessionModeTool());

        await AccessSaga().Handle(new ConnectToolCommand(ToolId, true), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(ToolId, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // The safe direction. Someone who agreed to hand over a session and lands on a tool that has
    // since dropped to score reads got strictly less than they consented to.
    [Fact]
    public async Task AcknowledgingASessionThatIsNoLongerAskedForStillConnects()
    {
        await AccessSaga().Handle(new ConnectToolCommand(ToolId, true), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(ToolId, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Tool SessionModeTool()
    {
        var tool = Tool.Create(ToolId, MakerId, Name.From("Tracker"), Now);
        tool.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0, hasOutboundHeader: true);
        return tool;
    }

    // Without this a maker has no real account to test against, and finding themselves in their
    // own directory would be a silly first step.
    [Fact]
    public async Task CreatingAToolConnectsItsMakerAsPlayerOne()
    {
        var saga = ManagementSaga();

        var id = await saga.Handle(new CreateToolCommand("Planner"), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(id, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private ToolManagementSaga ManagementSaga()
    {
        return new ToolManagementSaga(_tools.Object, _users.Object, _currentUser.Object,
            FakeDateTime.At(Now).Object, _mediator.Object, _secrets.Object, _webhooks.Object,
            Options.Create(new CommunityToolsConfiguration()), _repositories.Object, _bans.Object);
    }

    // Same guard owning a community carries. Without it: request deletion owning nothing, register
    // a tool on day three, and it evaporates on day seven taking its connected players with it.
    [Fact]
    public async Task AnAccountScheduledForDeletionCannotRegisterATool()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingAccountDeletionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PendingAccountDeletion(Now, Now.AddDays(7)));

        await Assert.ThrowsAsync<ToolListingException>(() => ManagementSaga()
            .Handle(new CreateToolCommand("Planner"), CancellationToken.None));

        _tools.Verify(t => t.Save(It.IsAny<Tool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancellingTheDeletionLetsThemRegisterAgain()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingAccountDeletionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAccountDeletion?)null);

        await ManagementSaga().Handle(new CreateToolCommand("Planner"), CancellationToken.None);

        _tools.Verify(t => t.Save(It.IsAny<Tool>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Rule 2's sanction. Deleting a tool never stopped its maker registering another thirty
    // seconds later, which is the entire reason the ban exists.
    [Fact]
    public async Task ABannedMakerCannotRegisterATool()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetPendingAccountDeletionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingAccountDeletion?)null);
        _bans.Setup(b => b.GetBan(MakerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolMakerBan(MakerId, Now, Guid.NewGuid(), null));

        await Assert.ThrowsAsync<ToolListingException>(() => ManagementSaga()
            .Handle(new CreateToolCommand("Planner"), CancellationToken.None));

        _tools.Verify(t => t.Save(It.IsAny<Tool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private ToolMakerBanSaga BanSaga(bool asAdmin = true)
    {
        _currentUser.SetupGet(c => c.User).Returns(new ScoreTracker.Domain.Models.User(
            asAdmin ? AdminId : MakerId, Name.From("DrMurloc"), true, null,
            new Uri("https://example.com/a.png"), null));

        return new ToolMakerBanSaga(_bans.Object, _currentUser.Object, FakeDateTime.At(Now).Object,
            _users.Object);
    }

    // Banning yourself would lock the only account that can lift it.
    [Fact]
    public async Task AnAdminCannotBanThemselves()
    {
        await Assert.ThrowsAsync<ToolListingException>(() => BanSaga()
            .Handle(new BanToolMakerCommand(AdminId, null), CancellationToken.None));

        _bans.Verify(b => b.Ban(It.IsAny<ToolMakerBan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABanRecordsWhoIssuedItAndWhen()
    {
        var target = Guid.NewGuid();

        await BanSaga().Handle(new BanToolMakerCommand(target, "  ads on the site  "),
            CancellationToken.None);

        _bans.Verify(b => b.Ban(It.Is<ToolMakerBan>(x =>
                x.UserId == target && x.BannedByUserId == AdminId && x.BannedAt == Now
                && x.Notes == "ads on the site"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BanningIsAdminOnly()
    {
        await Assert.ThrowsAsync<ToolNotFoundException>(() => BanSaga(asAdmin: false)
            .Handle(new BanToolMakerCommand(Guid.NewGuid(), null), CancellationToken.None));

        _bans.Verify(b => b.Ban(It.IsAny<ToolMakerBan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private readonly Mock<IToolActivityRepository> _activity = new();

    private ToolMetricsSaga MetricsSaga()
    {
        return new ToolMetricsSaga(_activity.Object, _tools.Object, FakeDateTime.At(Now).Object);
    }

    // Incremented into the hour's tally, never a row per click — a maker's own feed must not be
    // buried under traffic they did not generate.
    [Fact]
    public async Task AClickOnAListedToolIncrementsTheHourlyTally()
    {
        var listed = Tool.Create(ToolId, MakerId, Name.From("Pumpout"), Now,
            new Uri("https://github.com/errlena/pumpout"), "errlena", Now, ToolKind.ListingOnly);
        listed.MarkRepositoryReachable(Now);
        listed.Describe(Name.From("Pumpout"), "A chart database.",
            new Uri("https://pumpout.example"), new Uri("https://github.com/errlena/pumpout"));
        listed.MarkRepositoryReachable(Now);
        listed.RequestListing();
        listed.Approve(Now);
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>())).ReturnsAsync(listed);

        await MetricsSaga().Handle(new RecordToolClickCommand(ToolId), CancellationToken.None);

        _activity.Verify(a => a.Increment(ToolId, ToolActivityKind.DirectoryClicked, Now, null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // A click can only have come from the directory, so an unlisted tool accruing directory traffic
    // would mean something else is calling this.
    [Fact]
    public async Task AClickOnAnUnlistedToolIsNotCounted()
    {
        await MetricsSaga().Handle(new RecordToolClickCommand(ToolId), CancellationToken.None);

        _activity.Verify(a => a.Increment(It.IsAny<Guid>(), It.IsAny<ToolActivityKind>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void ToolWithRepository(bool alreadyChecked)
    {
        var tool = Tool.Create(ToolId, MakerId, Name.From("Planner"), Now,
            new Uri("https://github.com/errlena/planner"), "errlena", Now);
        if (alreadyChecked) tool.MarkRepositoryReachable(Now);
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>())).ReturnsAsync(tool);
    }

    [Fact]
    public async Task ARepositoryThatAnswersIsRecordedAsChecked()
    {
        ToolWithRepository(alreadyChecked: false);
        _repositories.Setup(r => r.Check(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryReachability.Ok(200));

        var result = await ManagementSaga()
            .Handle(new CheckToolRepositoryCommand(ToolId), CancellationToken.None);

        Assert.True(result.Reachable);
        _tools.Verify(t => t.Save(It.Is<Tool>(x => x.RepositoryCheckedAt == Now && x.CanBeSharedWithOthers),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // A repository that has gone private is the case this exists to catch, so a failing check has
    // to withdraw the previous proof. A stale tick beside a dead link is worse than no tick.
    [Fact]
    public async Task ARepositoryThatStoppedAnsweringLosesItsCheck()
    {
        ToolWithRepository(alreadyChecked: true);
        _repositories.Setup(r => r.Check(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(RepositoryReachability.Failed(WebhookFailureReason.ClientError, 404));

        var result = await ManagementSaga()
            .Handle(new CheckToolRepositoryCommand(ToolId), CancellationToken.None);

        Assert.False(result.Reachable);
        Assert.Equal(404, result.StatusCode);
        _tools.Verify(t => t.Save(It.Is<Tool>(x => x.RepositoryCheckedAt == null && !x.CanBeSharedWithOthers),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckingAToolWithNoRepositoryTellsTheMakerWhatToAdd()
    {
        await Assert.ThrowsAsync<ToolRepositoryRequiredException>(() => ManagementSaga()
            .Handle(new CheckToolRepositoryCommand(ToolId), CancellationToken.None));

        _repositories.Verify(r => r.Check(It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // The default tool in this fixture has no repository and no handle, and the current user is a
    // stranger to it.
    [Fact]
    public async Task AStrangerCannotConnectToAToolWithNoPublishedSource()
    {
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Tool.Create(ToolId, Guid.NewGuid(), Name.From("Planner"), Now));

        await Assert.ThrowsAsync<ToolRepositoryRequiredException>(() => AccessSaga()
            .Handle(new ConnectToolCommand(ToolId), CancellationToken.None));

        _tools.Verify(t => t.GrantShare(ToolId, MakerId, It.IsAny<ShareSource>(),
            It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ConnectingGrantsADirectShare above is the other half of this: its tool has no repository
    // either, and it passes because the connecting user is the maker. The gate is on acquiring a
    // second player, not on the tool working at all.
    [Fact]
    public async Task AStrangerConnectsOnceTheSourceIsPublishedAndChecked()
    {
        ToolWithRepository(alreadyChecked: true);

        await AccessSaga().Handle(new ConnectToolCommand(ToolId), CancellationToken.None);

        _tools.Verify(t => t.GrantShare(ToolId, MakerId, ShareSource.Direct, Now,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    ///     The owner read is ungated: a request made with a tool key has no signed-in user, so the
    ///     gated tool query cannot serve it, and the key already proves the caller acts for the tool.
    /// </summary>
    [Fact]
    public async Task ToolOwnerAnswersWithoutASignedInUserAndNullForNoSuchTool()
    {
        _currentUser.SetupGet(c => c.IsLoggedIn).Returns(false);
        _currentUser.SetupGet(c => c.User).Throws(new InvalidOperationException("no signed-in user"));
        var saga = ManagementSaga();

        Assert.Equal(MakerId, await saga.Handle(new GetToolOwnerQuery(ToolId), CancellationToken.None));
        Assert.Null(await saga.Handle(new GetToolOwnerQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
