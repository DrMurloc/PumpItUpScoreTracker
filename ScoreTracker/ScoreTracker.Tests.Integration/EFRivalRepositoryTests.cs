using ScoreTracker.Rivals.Domain;
using ScoreTracker.Rivals.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     The rival graph against real SQL. Everything here is about constraints and multi-statement
///     writes that only a real engine can answer: filtered unique indexes, a transactional block,
///     and a purge that has to erase an account from BOTH ends of a relationship.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFRivalRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFRivalRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private EFRivalRepository Rivals() => new(_fixture.DbContextFactory);
    private EFAccountPurgeRepository Purge() => new(_fixture.DbContextFactory);

    private static RivalEdge SiteEdge(Guid owner, Guid target) =>
        new(Guid.NewGuid(), owner, target, null, Now);

    private static RivalEdge TagEdge(Guid owner, string tag) =>
        new(Guid.NewGuid(), owner, null, tag, Now);

    [Fact]
    public async Task PersistsAndReadsBackBothDirections()
    {
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();
        await Rivals().Add(SiteEdge(me, them), CancellationToken.None);

        Assert.Single(await Rivals().GetRivalsOwnedBy(me, CancellationToken.None));
        Assert.Single(await Rivals().GetRivalsTargeting(them, CancellationToken.None));
        Assert.Empty(await Rivals().GetRivalsTargeting(me, CancellationToken.None));
    }

    /// <summary>
    ///     The reason both uniques are FILTERED. An unfiltered unique over (owner, user, tag) would
    ///     not constrain a tag edge at all, because in SQL Server a NULL never equals a NULL.
    /// </summary>
    [Fact]
    public async Task TheSameOwnerCannotStoreTheSameTagTwice()
    {
        var me = Guid.NewGuid();
        await Rivals().Add(TagEdge(me, "FRANKEZA#9606"), CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Rivals().Add(TagEdge(me, "FRANKEZA#9606"), CancellationToken.None));
    }

    [Fact]
    public async Task TheSameOwnerCannotStoreTheSameUserTwice()
    {
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();
        await Rivals().Add(SiteEdge(me, them), CancellationToken.None);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            Rivals().Add(SiteEdge(me, them), CancellationToken.None));
    }

    /// <summary>Different owners pointing at the same person is the normal case, not a collision.</summary>
    [Fact]
    public async Task TwoOwnersMayRivalTheSamePlayer()
    {
        var them = Guid.NewGuid();
        await Rivals().Add(SiteEdge(Guid.NewGuid(), them), CancellationToken.None);
        await Rivals().Add(SiteEdge(Guid.NewGuid(), them), CancellationToken.None);

        Assert.Equal(2, (await Rivals().GetRivalsTargeting(them, CancellationToken.None)).Count);
    }

    /// <summary>
    ///     A block that left the arrows standing would be a setting rather than a block, and the
    ///     row and the deletes have to land together.
    /// </summary>
    [Fact]
    public async Task BlockingDeletesBothArrowsAndIsReadableFromEitherSide()
    {
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();
        await Rivals().Add(SiteEdge(me, them), CancellationToken.None);
        await Rivals().Add(SiteEdge(them, me), CancellationToken.None);

        await Rivals().Block(me, them, Now, CancellationToken.None);

        Assert.Empty(await Rivals().GetRivalsOwnedBy(me, CancellationToken.None));
        Assert.Empty(await Rivals().GetRivalsOwnedBy(them, CancellationToken.None));
        Assert.True(await Rivals().IsBlockedEitherWay(me, them, CancellationToken.None));
        Assert.True(await Rivals().IsBlockedEitherWay(them, me, CancellationToken.None));
    }

    [Fact]
    public async Task BlockingTwiceIsTheSameAsBlockingOnce()
    {
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();

        await Rivals().Block(me, them, Now, CancellationToken.None);
        await Rivals().Block(me, them, Now, CancellationToken.None);

        Assert.Single(await Rivals().GetBlockedBy(me, CancellationToken.None));
    }

    [Fact]
    public async Task PromotingATagRewritesEveryEdgeHoldingIt()
    {
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        var linked = Guid.NewGuid();
        await Rivals().Add(TagEdge(one, "KAZE#4366"), CancellationToken.None);
        await Rivals().Add(TagEdge(two, "KAZE#4366"), CancellationToken.None);

        var promoted = await Rivals().PromoteTagToUser("KAZE#4366", linked, CancellationToken.None);

        Assert.Equal(2, promoted);
        Assert.Equal(2, (await Rivals().GetRivalsTargeting(linked, CancellationToken.None)).Count);
    }

    /// <summary>
    ///     Somebody may already rival the account directly, having found them on the site before
    ///     the tag linked. Promoting on top of that would trip the unique index, so the redundant
    ///     edge is dropped instead — same person either way.
    /// </summary>
    [Fact]
    public async Task PromotingDropsTheRedundantEdgeRatherThanColliding()
    {
        var me = Guid.NewGuid();
        var linked = Guid.NewGuid();
        await Rivals().Add(SiteEdge(me, linked), CancellationToken.None);
        await Rivals().Add(TagEdge(me, "KAZE#4366"), CancellationToken.None);

        await Rivals().PromoteTagToUser("KAZE#4366", linked, CancellationToken.None);

        Assert.Single(await Rivals().GetRivalsOwnedBy(me, CancellationToken.None));
    }

    /// <summary>Nobody rivals themselves — including after their own tag links to them.</summary>
    [Fact]
    public async Task PromotingDropsAnEdgeThatWouldBecomeASelfEdge()
    {
        var me = Guid.NewGuid();
        await Rivals().Add(TagEdge(me, "MYOWNTAG#1"), CancellationToken.None);

        await Rivals().PromoteTagToUser("MYOWNTAG#1", me, CancellationToken.None);

        Assert.Empty(await Rivals().GetRivalsOwnedBy(me, CancellationToken.None));
    }

    [Fact]
    public async Task RenamingRewritesTheTagAndCollapsesADuplicate()
    {
        var one = Guid.NewGuid();
        var two = Guid.NewGuid();
        await Rivals().Add(TagEdge(one, "OLD#1"), CancellationToken.None);
        await Rivals().Add(TagEdge(two, "OLD#1"), CancellationToken.None);
        // This owner already followed them under the new tag.
        await Rivals().Add(TagEdge(two, "NEW#2"), CancellationToken.None);

        await Rivals().RenameTag("OLD#1", "NEW#2", CancellationToken.None);

        Assert.Single(await Rivals().GetRivalsOwnedBy(one, CancellationToken.None));
        Assert.Single(await Rivals().GetRivalsOwnedBy(two, CancellationToken.None));
    }

    /// <summary>
    ///     The reason this vertical's purge is hand-written. UserDataPurge resolves ONE owning
    ///     *UserId column and throws on two; a rival edge carries two by design, and erasing only
    ///     the near end would leave a deleted player on somebody else's roster forever.
    /// </summary>
    [Fact]
    public async Task PurgingAnAccountErasesItFromBothEndsOfEveryRelationship()
    {
        var doomed = Guid.NewGuid();
        var other = Guid.NewGuid();
        await Rivals().Add(SiteEdge(doomed, other), CancellationToken.None);
        await Rivals().Add(SiteEdge(other, doomed), CancellationToken.None);
        await Rivals().Block(doomed, Guid.NewGuid(), Now, CancellationToken.None);
        await Rivals().Block(Guid.NewGuid(), doomed, Now, CancellationToken.None);

        await Purge().DeleteAllForUser(doomed, CancellationToken.None);

        Assert.Empty(await Rivals().GetRivalsOwnedBy(doomed, CancellationToken.None));
        Assert.Empty(await Rivals().GetRivalsTargeting(doomed, CancellationToken.None));
        Assert.Empty(await Rivals().GetRivalsOwnedBy(other, CancellationToken.None));
        Assert.False(await Rivals().IsBlockedEitherWay(doomed, other, CancellationToken.None));
    }

    [Fact]
    public async Task InviteCodesAreUniqueAndReplaceableInPlace()
    {
        var invites = new EFRivalInviteCodeRepository(_fixture.DbContextFactory);
        var me = Guid.NewGuid();
        var them = Guid.NewGuid();

        Assert.True(await invites.TrySetCode(me, "AAAA-BBBB-CCCC", Now, CancellationToken.None));
        // Somebody else's code is refused rather than stolen — handing a stranger the wrong
        // person's invite is the one outcome that must not happen.
        Assert.False(await invites.TrySetCode(them, "AAAA-BBBB-CCCC", Now, CancellationToken.None));

        Assert.True(await invites.TrySetCode(me, "DDDD-EEEE-FFFF", Now, CancellationToken.None));
        Assert.Equal("DDDD-EEEE-FFFF", await invites.GetCodeFor(me, CancellationToken.None));
        // Recycling kills the old link.
        Assert.Null(await invites.GetUserForCode("AAAA-BBBB-CCCC", CancellationToken.None));
        Assert.Equal(me, await invites.GetUserForCode("DDDD-EEEE-FFFF", CancellationToken.None));
    }
}
