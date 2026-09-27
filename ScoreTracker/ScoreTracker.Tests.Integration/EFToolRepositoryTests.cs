using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.Data.Persistence.Entities;
using ScoreTracker.Data.Repositories;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;
using ScoreTracker.Tests.Integration.TestData;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Who a tool may read, resolved against a real database.
///     <para>
///         The resolution is three sets combined in SQL — direct grants, the all-tools pool, and
///         blocks — with the pool gated on the maker's linked Discord account, and every rule in it
///         is a privacy rule. A mocked repository would only prove the handler asked; these prove the
///         queries and the Discord lookup answer, through the same <see cref="ToolReach" /> every
///         caller goes through.
///     </para>
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[ExcludeFromCodeCoverage]
public sealed class EFToolRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public EFToolRepositoryTests(SqlServerFixture fixture)
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

    private EFToolRepository BuildRepository()
    {
        return new EFToolRepository(_fixture.DbContextFactory);
    }

    private ToolReach Reach(EFToolRepository repository)
    {
        return new ToolReach(repository,
            new EFUserRepository(_fixture.DbContextFactory, new MemoryCache(new MemoryCacheOptions())));
    }

    /// <summary>A maker's account, with or without a Discord sign-in linked to it.</summary>
    private async Task<Guid> SeedMaker(bool discordLinked)
    {
        var maker = await new TestDataSeeder(_fixture.DbContextFactory).SeedUserAsync();
        if (!discordLinked) return maker;

        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        context.ExternalLogin.Add(new ExternalLoginEntity
        {
            LoginProvider = ToolReach.DiscordProvider,
            ExternalId = Random.Shared.NextInt64(100_000_000_000_000_000, 999_999_999_999_999_999).ToString(),
            UserId = maker
        });
        await context.SaveChangesAsync();
        return maker;
    }

    private async Task UnlinkDiscord(Guid maker)
    {
        await using var context = await _fixture.DbContextFactory.CreateDbContextAsync();
        await context.ExternalLogin
            .Where(e => e.UserId == maker && e.LoginProvider == ToolReach.DiscordProvider)
            .ExecuteDeleteAsync();
    }

    private async Task<Tool> SaveTool(EFToolRepository repository, string name, bool makerHasDiscord = true,
        Action<Tool>? configure = null)
    {
        var tool = Tool.Create(Guid.NewGuid(), await SeedMaker(makerHasDiscord), Name.From(name), Now);
        configure?.Invoke(tool);
        await repository.Save(tool);
        return tool;
    }

    private static void PublicAndPooled(Tool tool)
    {
        tool.Describe(Name.From(tool.Name.ToString()), "A tool", new Uri("https://example.com"),
            new Uri("https://github.com/errlena/a-tool"));
        tool.RequestListing(makerHasDiscord: true);
        tool.Approve(Now);
        tool.SetAcceptsAllToolsShare(true);
    }

    /// <summary>
    ///     The rule that matters most in the whole vertical. A session-mode tool is handed the key we
    ///     sign in to piugame.com with; blanket consent must never be a route to it, however public
    ///     and however approved the tool is.
    /// </summary>
    [Fact]
    public async Task ASessionModeToolIsNeverReachedByBlanketConsent()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var scorePush = await SaveTool(repository, "Planner", configure: t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.ScorePush, new Uri("https://planner.example/hook"), 0, hasOutboundHeader: true);
        });
        var session = await SaveTool(repository, "Tracker", configure: t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0, hasOutboundHeader: true);
        });

        var reading = await reach.ToolIdsReading(player, CancellationToken.None);

        Assert.Contains(scorePush.Id, reading);
        Assert.DoesNotContain(session.Id, reading);
        Assert.False(await reach.CanRead(session.Id, player, CancellationToken.None));
        Assert.DoesNotContain(player, await reach.ReadablePlayerIds(session.Id, CancellationToken.None));
    }

    /// <summary>
    ///     The maker gate, asserted where it is actually enforced: nothing routes through a handler
    ///     to grant the pool, so only the real resolution can catch an over-permissive answer — the
    ///     same reason <c>AccountPurgeTests</c> exists.
    /// </summary>
    [Fact]
    public async Task AToolWhoseMakerHasNoDiscordIsNeverReachedByBlanketConsent()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var linked = await SaveTool(repository, "Planner", configure: PublicAndPooled);
        var unlinked = await SaveTool(repository, "Digger", makerHasDiscord: false, configure: PublicAndPooled);

        var reading = await reach.ToolIdsReading(player, CancellationToken.None);

        Assert.Contains(linked.Id, reading);
        Assert.DoesNotContain(unlinked.Id, reading);
        Assert.False(await reach.CanRead(unlinked.Id, player, CancellationToken.None));
        Assert.DoesNotContain(player, await reach.ReadablePlayerIds(unlinked.Id, CancellationToken.None));
    }

    /// <summary>
    ///     A listing-only tool is a link in the directory: no key, no endpoint, nobody it can read. It
    ///     is created accepting the pool like every tool, so the query has to leave it out, or a
    ///     sharing player is told it is one of the tools reading their scores.
    /// </summary>
    [Fact]
    public async Task AListingOnlyToolIsNeverReachedByBlanketConsent()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var integrated = await SaveTool(repository, "Planner", configure: PublicAndPooled);
        var listing = Tool.Create(Guid.NewGuid(), await SeedMaker(discordLinked: true), Name.From("Sheet"), Now,
            kind: ToolKind.ListingOnly);
        PublicAndPooled(listing);
        await repository.Save(listing);

        var reading = await reach.ToolIdsReading(player, CancellationToken.None);

        Assert.Contains(integrated.Id, reading);
        Assert.DoesNotContain(listing.Id, reading);
        Assert.False(await reach.CanRead(listing.Id, player, CancellationToken.None));
        Assert.DoesNotContain(player, await reach.ReadablePlayerIds(listing.Id, CancellationToken.None));
        Assert.Equal(0, await reach.CountConnectedPlayers(listing.Id, CancellationToken.None));
    }

    /// <summary>
    ///     A ban disables rather than deletes, so its effect has to be computed at read time — which
    ///     means only a real query can prove it happened. The shares stay in the table untouched,
    ///     which is what makes the ban liftable.
    /// </summary>
    [Fact]
    public async Task ABannedMakersToolReadsNobody()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var bans = new EFToolMakerBanRepository(_fixture.DbContextFactory);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var tool = await SaveTool(repository, "Digger", configure: PublicAndPooled);
        var deliberate = Guid.NewGuid();
        await repository.GrantShare(tool.Id, deliberate, ShareSource.Direct, Now);

        Assert.True(await reach.CanRead(tool.Id, deliberate, CancellationToken.None));

        await bans.Ban(new ToolMakerBan(tool.OwnerUserId, Now, Guid.NewGuid(), "ads on the site"));

        Assert.Empty(await reach.ReadablePlayerIds(tool.Id, CancellationToken.None));
        Assert.False(await reach.CanRead(tool.Id, deliberate, CancellationToken.None));
        Assert.DoesNotContain(tool.Id, await reach.ToolIdsReading(player, CancellationToken.None));

        // And the grant was never touched, so lifting restores a working tool.
        await bans.Lift(tool.OwnerUserId);

        Assert.True(await reach.CanRead(tool.Id, deliberate, CancellationToken.None));
        Assert.Contains(tool.Id, await reach.ToolIdsReading(player, CancellationToken.None));
    }

    /// <summary>
    ///     The site's own rule: losing the blanket grant never takes a deliberate named one with it.
    ///     Cutting off players who chose a tool because its maker unlinked an account would punish the
    ///     wrong people.
    /// </summary>
    [Fact]
    public async Task ADeliberateGrantSurvivesTheMakerUnlinkingDiscord()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var pooled = Guid.NewGuid();
        var granted = Guid.NewGuid();
        await repository.SetShareWithAllTools(pooled, true, Now);

        var tool = await SaveTool(repository, "Digger", configure: PublicAndPooled);
        await repository.GrantShare(tool.Id, granted, ShareSource.Direct, Now);
        Assert.True(await reach.CanRead(tool.Id, pooled, CancellationToken.None));

        await UnlinkDiscord(tool.OwnerUserId);

        Assert.True(await reach.CanRead(tool.Id, granted, CancellationToken.None));
        Assert.False(await reach.CanRead(tool.Id, pooled, CancellationToken.None));
    }

    /// <summary>
    ///     The other half: excluding session mode from the pool must not exclude it from the grants a
    ///     player made deliberately. That is the only route it has.
    /// </summary>
    [Fact]
    public async Task ASessionModeToolStillReadsThePlayersWhoGrantedItDirectly()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        var session = await SaveTool(repository, "Tracker", configure: t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0, hasOutboundHeader: true);
        });

        await repository.GrantShare(session.Id, player, ShareSource.Direct, Now);

        Assert.True(await reach.CanRead(session.Id, player, CancellationToken.None));
        Assert.Contains(session.Id, await reach.ToolIdsReading(player, CancellationToken.None));
    }

    [Fact]
    public async Task ABlockRemovesOnePooledToolAndLeavesTheRest()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var kept = await SaveTool(repository, "Planner", configure: PublicAndPooled);
        var blocked = await SaveTool(repository, "Unwanted", configure: PublicAndPooled);
        await repository.BlockTool(blocked.Id, player, Now);

        var reading = await reach.ToolIdsReading(player, CancellationToken.None);

        Assert.Contains(kept.Id, reading);
        Assert.DoesNotContain(blocked.Id, reading);
    }

    /// <summary>
    ///     A tool that opted out of the pool keeps the players who chose it. Opting out is a maker
    ///     saying "I don't want strangers' data", not "drop the users I have".
    /// </summary>
    [Fact]
    public async Task OptingOutOfThePoolKeepsDirectGrants()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var pooled = Guid.NewGuid();
        var granted = Guid.NewGuid();
        await repository.SetShareWithAllTools(pooled, true, Now);

        var tool = await SaveTool(repository, "Picky", configure: t =>
        {
            PublicAndPooled(t);
            t.SetAcceptsAllToolsShare(false);
        });
        await repository.GrantShare(tool.Id, granted, ShareSource.Direct, Now);

        var readable = await reach.ReadablePlayerIds(tool.Id, CancellationToken.None);

        Assert.Contains(granted, readable);
        Assert.DoesNotContain(pooled, readable);
    }

    /// <summary>
    ///     Listing is a directory concern and never an access one: a private tool whose maker has
    ///     Discord linked is in the pool on the same terms as a listed one — with no source published.
    /// </summary>
    [Fact]
    public async Task APrivateToolIsInThePoolOnTheSameTermsAsAListedOne()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var unlisted = await SaveTool(repository, "Unlisted");

        Assert.Equal(ToolVisibility.Private, unlisted.Visibility);
        Assert.Null(unlisted.RepositoryUrl);
        Assert.True(await reach.CanRead(unlisted.Id, player, CancellationToken.None));
        Assert.Contains(player, await reach.ReadablePlayerIds(unlisted.Id, CancellationToken.None));
        Assert.Contains(unlisted.Id, await reach.ToolIdsReading(player, CancellationToken.None));
    }

    /// <summary>
    ///     The other half, and the reason leaving visibility out is safe: what holds the line is a
    ///     reachable maker, and an unlisted tool is held to it exactly as a listed one is.
    /// </summary>
    [Fact]
    public async Task APrivateToolWhoseMakerHasNoDiscordIsNotInThePool()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var tool = await SaveTool(repository, "Unlisted", makerHasDiscord: false);

        Assert.False(await reach.CanRead(tool.Id, player, CancellationToken.None));
        Assert.DoesNotContain(tool.Id, await reach.ToolIdsReading(player, CancellationToken.None));
    }

    /// <summary>
    ///     Revoking must actually stop reads rather than only hiding the row, and re-connecting must
    ///     work — a player who changes their mind twice is ordinary.
    /// </summary>
    [Fact]
    public async Task RevokingStopsReadsAndReconnectingResumesThem()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var player = Guid.NewGuid();
        var tool = await SaveTool(repository, "Planner");

        await repository.GrantShare(tool.Id, player, ShareSource.Direct, Now);
        await repository.RevokeShare(tool.Id, player, Now.AddMinutes(1));
        Assert.False(await reach.CanRead(tool.Id, player, CancellationToken.None));

        await repository.GrantShare(tool.Id, player, ShareSource.Direct, Now.AddMinutes(2));
        Assert.True(await reach.CanRead(tool.Id, player, CancellationToken.None));
    }

    /// <summary>
    ///     The count the session-mode gate reads. It has to include the pool, or a public tool with
    ///     two hundred pooled players could flip into session mode and inherit every one of them.
    /// </summary>
    [Fact]
    public async Task ConnectedPlayerCountIncludesThePool()
    {
        var repository = BuildRepository();
        var reach = Reach(repository);
        var pooled = Guid.NewGuid();
        var direct = Guid.NewGuid();
        await repository.SetShareWithAllTools(pooled, true, Now);

        var tool = await SaveTool(repository, "Planner", configure: PublicAndPooled);
        await repository.GrantShare(tool.Id, direct, ShareSource.Direct, Now);

        Assert.Equal(2, await reach.CountConnectedPlayers(tool, CancellationToken.None));
    }
}
