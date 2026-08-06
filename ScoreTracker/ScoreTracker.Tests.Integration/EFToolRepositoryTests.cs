using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.CommunityTools.Infrastructure;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.Integration.Fixtures;

namespace ScoreTracker.Tests.Integration;

/// <summary>
///     Who a tool may read, resolved against a real database.
///     <para>
///         The resolution is three sets combined in SQL — direct grants, the all-tools pool, and
///         blocks — and every rule in it is a privacy rule. A mocked repository would only prove the
///         handler asked; these prove the query answers.
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

    private async Task<Tool> SaveTool(EFToolRepository repository, string name,
        Action<Tool>? configure = null)
    {
        var tool = Tool.Create(Guid.NewGuid(), Guid.NewGuid(), Name.From(name), Now);
        configure?.Invoke(tool);
        await repository.Save(tool);
        return tool;
    }

    private static void PublicAndPooled(Tool tool)
    {
        Sourced(tool);
        tool.RequestListing();
        tool.Approve(Now);
        tool.SetAcceptsAllToolsShare(true);
    }

    /// <summary>Everything the source-and-maker gate asks for, so listing is allowed.</summary>
    private static void Sourced(Tool tool)
    {
        tool.Describe(Name.From(tool.Name.ToString()), "A tool", new Uri("https://example.com"),
            new Uri("https://github.com/errlena/a-tool"));
        tool.SetDiscordHandle("errlena");
        tool.MarkRepositoryReachable(Now);
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
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var scorePush = await SaveTool(repository, "Planner", t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.ScorePush, new Uri("https://planner.example/hook"), 0, hasOutboundHeader: true);
        });
        var session = await SaveTool(repository, "Tracker", t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0, hasOutboundHeader: true);
        });

        var reading = await repository.GetToolIdsReading(player);

        Assert.Contains(scorePush.Id, reading);
        Assert.DoesNotContain(session.Id, reading);
        Assert.False(await repository.CanRead(session.Id, player));
        Assert.DoesNotContain(player, await repository.GetReadablePlayerIds(session.Id));
    }

    /// <summary>
    ///     The source-and-maker gate, asserted where it is actually enforced.
    ///     <para>
    ///         Effective access is three sets combined in SQL against the entity, and nothing routes
    ///         through a handler to grant the pool — so the handler-level refusal produces a good
    ///         error message over a pool that would still be wide open. Only a real query can catch
    ///         an over-permissive predicate, which is the same reason <c>AccountPurgeTests</c> exists.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task AToolWithNoCheckedSourceIsNeverReachedByBlanketConsent()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var sourced = await SaveTool(repository, "Planner", PublicAndPooled);
        var unsourced = await SaveTool(repository, "Digger", t =>
        {
            PublicAndPooled(t);
            // Approved, pooled, and then the source link changed — which withdraws its check.
            t.Describe(Name.From("Digger"), "A tool", new Uri("https://example.com"),
                new Uri("https://github.com/errlena/moved-somewhere-else"));
        });

        Assert.Contains(sourced.Id, await repository.GetToolIdsReading(player));
        Assert.DoesNotContain(unsourced.Id, await repository.GetToolIdsReading(player));
        Assert.False(await repository.CanRead(unsourced.Id, player));
        Assert.DoesNotContain(player, await repository.GetReadablePlayerIds(unsourced.Id));
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
        var bans = new EFToolMakerBanRepository(_fixture.DbContextFactory);
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var tool = await SaveTool(repository, "Digger", PublicAndPooled);
        var deliberate = Guid.NewGuid();
        await repository.GrantShare(tool.Id, deliberate, ShareSource.Direct, Now);

        Assert.True(await repository.CanRead(tool.Id, deliberate));

        await bans.Ban(new ToolMakerBan(tool.OwnerUserId, Now, Guid.NewGuid(), "ads on the site"));

        Assert.Empty(await repository.GetReadablePlayerIds(tool.Id));
        Assert.False(await repository.CanRead(tool.Id, deliberate));
        Assert.DoesNotContain(tool.Id, await repository.GetToolIdsReading(player));

        // And the grant was never touched, so lifting restores a working tool.
        await bans.Lift(tool.OwnerUserId);

        Assert.True(await repository.CanRead(tool.Id, deliberate));
        Assert.Contains(tool.Id, await repository.GetToolIdsReading(player));
    }

    /// <summary>
    ///     The other half, and the site's own rule: going private blocks the blanket grant and never
    ///     a deliberate named one. Cutting off players who chose a tool because its maker mistyped a
    ///     URL would punish the wrong people.
    /// </summary>
    [Fact]
    public async Task ADeliberateGrantSurvivesTheSourceGate()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();

        var tool = await SaveTool(repository, "Digger", PublicAndPooled);
        await repository.GrantShare(tool.Id, player, ShareSource.Direct, Now);

        tool.Describe(Name.From("Digger"), "A tool", new Uri("https://example.com"),
            new Uri("https://github.com/errlena/moved-somewhere-else"));
        await repository.Save(tool);

        Assert.True(await repository.CanRead(tool.Id, player));
        Assert.Contains(player, await repository.GetReadablePlayerIds(tool.Id));
    }

    /// <summary>
    ///     The other half: excluding session mode from the pool must not exclude it from the grants a
    ///     player made deliberately. That is the only route it has.
    /// </summary>
    [Fact]
    public async Task ASessionModeToolStillReadsThePlayersWhoGrantedItDirectly()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        var session = await SaveTool(repository, "Tracker", t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0, hasOutboundHeader: true);
        });

        await repository.GrantShare(session.Id, player, ShareSource.Direct, Now);

        Assert.True(await repository.CanRead(session.Id, player));
        Assert.Contains(session.Id, await repository.GetToolIdsReading(player));
    }

    [Fact]
    public async Task ABlockRemovesOnePooledToolAndLeavesTheRest()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var kept = await SaveTool(repository, "Planner", PublicAndPooled);
        var blocked = await SaveTool(repository, "Unwanted", PublicAndPooled);
        await repository.BlockTool(blocked.Id, player, Now);

        var reading = await repository.GetToolIdsReading(player);

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
        var pooled = Guid.NewGuid();
        var granted = Guid.NewGuid();
        await repository.SetShareWithAllTools(pooled, true, Now);

        var tool = await SaveTool(repository, "Picky", t =>
        {
            PublicAndPooled(t);
            t.SetAcceptsAllToolsShare(false);
        });
        await repository.GrantShare(tool.Id, granted, ShareSource.Direct, Now);

        var readable = await repository.GetReadablePlayerIds(tool.Id);

        Assert.Contains(granted, readable);
        Assert.DoesNotContain(pooled, readable);
    }

    /// <summary>
    ///     Listing is a directory concern and never an access one: a private tool that publishes its
    ///     source and names a reachable maker is in the pool on the same terms as a listed one.
    ///     <para>
    ///         Asserted against a real query rather than a handler, because nothing routes through a
    ///         handler to grant the pool — the predicate <i>is</i> the rule.
    ///     </para>
    /// </summary>
    [Fact]
    public async Task APrivateToolIsInThePoolOnTheSameTermsAsAListedOne()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var unlisted = await SaveTool(repository, "Unlisted", t =>
        {
            Sourced(t);
            t.SetAcceptsAllToolsShare(true);
        });

        Assert.Equal(ToolVisibility.Private, unlisted.Visibility);
        Assert.True(await repository.CanRead(unlisted.Id, player));
        Assert.Contains(player, await repository.GetReadablePlayerIds(unlisted.Id));
        Assert.Contains(unlisted.Id, await repository.GetToolIdsReading(player));
    }

    /// <summary>
    ///     The other half, and the reason dropping the visibility gate is safe: what actually holds
    ///     the line is the published source and the reachable maker, and an unlisted tool is held to
    ///     it exactly as a listed one is.
    /// </summary>
    [Fact]
    public async Task APrivateToolWithNoCheckedSourceIsStillNotInThePool()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var tool = await SaveTool(repository, "Unlisted", t => t.SetAcceptsAllToolsShare(true));

        Assert.False(await repository.CanRead(tool.Id, player));
        Assert.DoesNotContain(tool.Id, await repository.GetToolIdsReading(player));
    }

    /// <summary>
    ///     Revoking must actually stop reads rather than only hiding the row, and re-connecting must
    ///     work — a player who changes their mind twice is ordinary.
    /// </summary>
    [Fact]
    public async Task RevokingStopsReadsAndReconnectingResumesThem()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        var tool = await SaveTool(repository, "Planner");

        await repository.GrantShare(tool.Id, player, ShareSource.Direct, Now);
        await repository.RevokeShare(tool.Id, player, Now.AddMinutes(1));
        Assert.False(await repository.CanRead(tool.Id, player));

        await repository.GrantShare(tool.Id, player, ShareSource.Direct, Now.AddMinutes(2));
        Assert.True(await repository.CanRead(tool.Id, player));
    }

    /// <summary>
    ///     The count the session-mode gate reads. It has to include the pool, or a public tool with
    ///     two hundred pooled players could flip into session mode and inherit every one of them.
    /// </summary>
    [Fact]
    public async Task ConnectedPlayerCountIncludesThePool()
    {
        var repository = BuildRepository();
        var pooled = Guid.NewGuid();
        var direct = Guid.NewGuid();
        await repository.SetShareWithAllTools(pooled, true, Now);

        var tool = await SaveTool(repository, "Planner", PublicAndPooled);
        await repository.GrantShare(tool.Id, direct, ShareSource.Direct, Now);

        Assert.Equal(2, await repository.CountConnectedPlayers(tool.Id));
    }
}
