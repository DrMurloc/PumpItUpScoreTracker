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
        tool.Describe(Name.From(tool.Name.ToString()), "A tool", new Uri("https://example.com"));
        tool.RequestListing();
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
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var scorePush = await SaveTool(repository, "Planner", t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.ScorePush, new Uri("https://planner.example/hook"), 0);
        });
        var session = await SaveTool(repository, "Tracker", t =>
        {
            PublicAndPooled(t);
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0);
        });

        var reading = await repository.GetToolIdsReading(player);

        Assert.Contains(scorePush.Id, reading);
        Assert.DoesNotContain(session.Id, reading);
        Assert.False(await repository.CanRead(session.Id, player));
        Assert.DoesNotContain(player, await repository.GetReadablePlayerIds(session.Id));
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
            t.SetWebhook(WebhookMode.PiuGameSession, new Uri("https://tracker.example/hook"), 0);
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
    ///     A private tool is invite-only whatever a player's blanket preference says — approval is
    ///     what puts a tool in front of everyone, and it hasn't happened.
    /// </summary>
    [Fact]
    public async Task APrivateToolIsNotInThePoolEvenWhenItAcceptsIt()
    {
        var repository = BuildRepository();
        var player = Guid.NewGuid();
        await repository.SetShareWithAllTools(player, true, Now);

        var tool = await SaveTool(repository, "Unlisted", t => t.SetAcceptsAllToolsShare(true));

        Assert.False(await repository.CanRead(tool.Id, player));
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
