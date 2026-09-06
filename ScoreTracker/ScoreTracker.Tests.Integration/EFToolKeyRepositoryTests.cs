using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Keys and invite links against a real database.
///     <para>
///         The note on an invite link is the maker's own reminder of where they posted it, so it has
///         to survive a round trip through a real column with real collation — an in-memory
///         repository would prove nothing about a nullable nvarchar that a migration just added.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFToolKeyRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid AToolId = Guid.Parse("dddddddd-0000-0000-0000-00000000000d");

    private readonly SqlServerFixture _fixture;

    public EFToolKeyRepositoryTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    private EFToolKeyRepository Repository => new(_fixture.DbContextFactory);

    [Fact]
    public async Task AnInviteLinkStartsWithNoNoteAndKeepsOneOnceSet()
    {
        var code = Guid.NewGuid();
        await Repository.AddInviteCode(AToolId, code, Now);

        var fresh = Assert.Single(await Repository.GetInviteCodes(AToolId));
        Assert.Null(fresh.Note);

        await Repository.SetInviteCodeNote(AToolId, code, "posted in #tools");

        var noted = Assert.Single(await Repository.GetInviteCodes(AToolId));
        Assert.Equal("posted in #tools", noted.Note);
        Assert.Equal(code, noted.Code);
    }

    [Fact]
    public async Task ANoteCanBeClearedAgain()
    {
        var code = Guid.NewGuid();
        await Repository.AddInviteCode(AToolId, code, Now);
        await Repository.SetInviteCodeNote(AToolId, code, "somewhere");

        await Repository.SetInviteCodeNote(AToolId, code, null);

        Assert.Null(Assert.Single(await Repository.GetInviteCodes(AToolId)).Note);
    }

    /// <summary>
    ///     The tool id is part of the predicate, not decoration: a maker must not be able to write
    ///     onto someone else's link by knowing its code.
    /// </summary>
    [Fact]
    public async Task ANoteIsNotWrittenThroughTheWrongTool()
    {
        var code = Guid.NewGuid();
        await Repository.AddInviteCode(AToolId, code, Now);

        await Repository.SetInviteCodeNote(Guid.NewGuid(), code, "not yours");

        Assert.Null(Assert.Single(await Repository.GetInviteCodes(AToolId)).Note);
    }

    /// <summary>A revoked link is gone from the console, note and all.</summary>
    [Fact]
    public async Task ARevokedLinkIsNoLongerListed()
    {
        var code = Guid.NewGuid();
        await Repository.AddInviteCode(AToolId, code, Now);
        await Repository.SetInviteCodeNote(AToolId, code, "old crew");

        await Repository.RevokeInviteCode(AToolId, code, Now.AddHours(1));

        Assert.Empty(await Repository.GetInviteCodes(AToolId));
        Assert.Null(await Repository.ResolveToolByInviteCode(code));
    }

    /// <summary>
    ///     Oldest first, so the list does not reshuffle under the maker when they edit a note —
    ///     which is exactly when they are reading the row above it.
    /// </summary>
    [Fact]
    public async Task LinksComeBackOldestFirst()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await Repository.AddInviteCode(AToolId, first, Now);
        await Repository.AddInviteCode(AToolId, second, Now.AddMinutes(5));

        var codes = (await Repository.GetInviteCodes(AToolId)).Select(l => l.Code).ToArray();

        Assert.Equal(new[] { first, second }, codes);
    }

    /// <summary>
    ///     Rotating the minted prefix must not invalidate a key someone is already holding: the
    ///     stored hash covers the whole string, so both shapes have to reach the database.
    /// </summary>
    [Fact]
    public async Task AKeyOnEitherPrefixResolvesToItsTool()
    {
        var minted = ApiKeyMint.Mint();
        Assert.StartsWith("piu_scores_live_", minted.Key, StringComparison.Ordinal);

        const string legacy = "pst_live_" +
                              "4f8c21ab90de7715c3a06b28f4e15d934f8c21ab90de7715c3a06b28f4e15d93";
        Assert.True(ApiKeyMint.LooksLikeAKey(minted.Key));
        Assert.True(ApiKeyMint.LooksLikeAKey(legacy));
        Assert.False(ApiKeyMint.LooksLikeAKey("piu_scores_live_short"));

        await Repository.AddKey(AToolId, Guid.NewGuid(), "new", minted.Hash, minted.Last4, Now, null);
        await Repository.AddKey(AToolId, Guid.NewGuid(), "old", ApiKeyMint.HashOf(legacy),
            legacy[^4..], Now, null);

        Assert.Equal(AToolId, (await Repository.ResolveToolByKeyHash(ApiKeyMint.HashOf(minted.Key), Now))?.ToolId);
        Assert.Equal(AToolId, (await Repository.ResolveToolByKeyHash(ApiKeyMint.HashOf(legacy), Now))?.ToolId);
    }

    /// <summary>
    ///     A live key is stamped on every resolve — the maker's only proof it carries traffic — and
    ///     a revoked one resolves to nothing at all, which is indistinguishable from a key that
    ///     never existed.
    /// </summary>
    [Fact]
    public async Task ALiveKeyIsNamedAndStampedAndARevokedOneIsNothing()
    {
        var live = ApiKeyMint.Mint();
        var revoked = ApiKeyMint.Mint();
        var revokedId = Guid.NewGuid();
        await Repository.AddKey(AToolId, Guid.NewGuid(), "live", live.Hash, live.Last4, Now, null);
        await Repository.AddKey(AToolId, revokedId, "gone", revoked.Hash, revoked.Last4, Now, null);
        await Repository.RevokeKey(AToolId, revokedId, Now);

        var resolution = await Repository.ResolveToolByKeyHash(live.Hash, Now.AddMinutes(5));

        Assert.NotNull(resolution);
        Assert.Equal("live", resolution!.KeyName);
        Assert.False(resolution.IsExpired);
        Assert.Null(await Repository.ResolveToolByKeyHash(revoked.Hash, Now.AddMinutes(5)));
        var keys = await Repository.GetKeys(AToolId);
        Assert.Equal(Now.AddMinutes(5), keys.Single(k => k.Name == "live").LastUsedAt);
        Assert.Null(keys.Single(k => k.Name == "gone").LastUsedAt);
    }

    /// <summary>Named so the console can say which key bounced, but never stamped: it carried nothing.</summary>
    [Fact]
    public async Task AnExpiredKeyIsNamedButNotStamped()
    {
        var minted = ApiKeyMint.Mint();
        await Repository.AddKey(AToolId, Guid.NewGuid(), "old", minted.Hash, minted.Last4,
            Now.AddDays(-30), Now.AddDays(-1));

        var resolution = await Repository.ResolveToolByKeyHash(minted.Hash, Now);

        Assert.NotNull(resolution);
        Assert.True(resolution!.IsExpired);
        Assert.Equal("old", resolution.KeyName);
        Assert.Equal(AToolId, resolution.ToolId);
        Assert.Null(Assert.Single(await Repository.GetKeys(AToolId)).LastUsedAt);
    }
}
