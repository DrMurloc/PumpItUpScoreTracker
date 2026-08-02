using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ScoreTracker.CommunityTools.Application;
using ScoreTracker.CommunityTools.Contracts;
using ScoreTracker.CommunityTools.Domain;
using ScoreTracker.Domain.Events;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.ValueTypes;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>The fan-out, the payload shape, and the rules about what may be written down.</summary>
public sealed class WebhookDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ToolId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Uri Hook = new("https://pumbility.app/hooks/piuscores");

    private readonly Mock<IToolRepository> _tools = new();
    private readonly Mock<IWebhookDeliveryDispatcher> _dispatcher = new();
    private readonly Mock<IUserReader> _users = new();

    private static Tool ToolWith(WebhookMode mode, params MixEnum[] mixes)
    {
        var tool = Tool.Create(ToolId, Guid.NewGuid(), Name.From("Planner"), Now);
        tool.SetWebhook(mode, mode == WebhookMode.None ? null : Hook, 0);
        tool.SetMixes(mixes);
        return tool;
    }

    private WebhookDeliverySaga Saga()
    {
        _users.Setup(u => u.GetUser(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User(UserId, Name.From("DrMurloc"), true, Name.From("MURLOC#1"),
                new Uri("https://example.com/a.png"), null));
        return new WebhookDeliverySaga(_tools.Object, _dispatcher.Object, _users.Object,
            NullLogger<WebhookDeliverySaga>.Instance);
    }

    private static ConsumeContext<PlayerScoresUpdatedEvent> Batch(MixEnum mix, int changeCount)
    {
        var changes = Enumerable.Range(0, changeCount)
            .Select(i => new PlayerScoresUpdatedEvent.ScoreChange(Guid.NewGuid(), false, 900000,
                900000 + i, "FairGame", false))
            .ToArray();
        var context = new Mock<ConsumeContext<PlayerScoresUpdatedEvent>>();
        context.SetupGet(c => c.Message)
            .Returns(PlayerScoresUpdatedEvent.Create(Now, UserId, mix, changes));
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private void SetupTool(Tool tool)
    {
        _tools.Setup(t => t.GetToolIdsReading(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ToolId });
        _tools.Setup(t => t.GetTool(ToolId, It.IsAny<CancellationToken>())).ReturnsAsync(tool);
    }

    [Fact]
    public async Task ScorePushCarriesTheChangesAndPingCarriesNone()
    {
        SetupTool(ToolWith(WebhookMode.ScorePush));
        await Saga().Consume(Batch(MixEnum.Phoenix, 3));
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<Tool>(), It.IsAny<DeliveryPayload.PlayerBlock>(),
            It.IsAny<Guid?>(), It.Is<IReadOnlyList<DeliveryPayload.Change>>(c => c.Count == 3),
            false, false, It.IsAny<CancellationToken>()), Times.Once);

        _dispatcher.Reset();
        SetupTool(ToolWith(WebhookMode.PlayerPing));
        await Saga().Consume(Batch(MixEnum.Phoenix, 3));
        _dispatcher.Verify(d => d.Dispatch(It.IsAny<Tool>(), It.IsAny<DeliveryPayload.PlayerBlock>(),
            It.IsAny<Guid?>(), It.Is<IReadOnlyList<DeliveryPayload.Change>>(c => c.Count == 0),
            false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    // A first-time import is thousands of scores; pushing them all would be thirty POSTs for one
    // player. Page one plus a link caps what we send by construction.
    [Fact]
    public async Task ABatchOverTheChunkSizeSendsOnePageAndFlagsMore()
    {
        SetupTool(ToolWith(WebhookMode.ScorePush));

        await Saga().Consume(Batch(MixEnum.Phoenix, 250));

        _dispatcher.Verify(d => d.Dispatch(It.IsAny<Tool>(), It.IsAny<DeliveryPayload.PlayerBlock>(),
            It.IsAny<Guid?>(),
            It.Is<IReadOnlyList<DeliveryPayload.Change>>(c => c.Count == WebhookDeliverySaga.MaxChangesPerDelivery),
            true, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AToolOnlySubscribedToOtherMixesIsSkipped()
    {
        SetupTool(ToolWith(WebhookMode.ScorePush, MixEnum.Phoenix2));

        await Saga().Consume(Batch(MixEnum.Phoenix, 1));

        _dispatcher.VerifyNoOtherCalls();
    }

    // Session mode is delivered inline during the import, where the sid exists. This consumer runs
    // after the fact and has no credential to forward.
    [Fact]
    public async Task SessionModeIsNeverDeliveredFromTheScoreBatch()
    {
        SetupTool(ToolWith(WebhookMode.PiuGameSession));

        await Saga().Consume(Batch(MixEnum.Phoenix, 1));

        _dispatcher.VerifyNoOtherCalls();
    }

    // A Fiesta EX number is era-scale and does not compare to a Phoenix score, so the score slots
    // stay empty and the letter grade carries the meaning.
    [Fact]
    public async Task ALegacyMixSendsLetterGradesRatherThanScores()
    {
        SetupTool(ToolWith(WebhookMode.ScorePush));
        IReadOnlyList<DeliveryPayload.Change>? captured = null;
        _dispatcher.Setup(d => d.Dispatch(It.IsAny<Tool>(), It.IsAny<DeliveryPayload.PlayerBlock>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<DeliveryPayload.Change>>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((Tool _, DeliveryPayload.PlayerBlock _, Guid? _, IReadOnlyList<DeliveryPayload.Change> c,
                bool _, bool _, CancellationToken _) => captured = c)
            .Returns(Task.CompletedTask);

        await Saga().Consume(Batch(MixEnum.FiestaEx, 1));

        var change = Assert.Single(captured!);
        Assert.Null(change.NewScore);
        Assert.NotNull(change.NewLetterGrade);
    }

    [Fact]
    public async Task TheScoringModelRidesThePlayerBlock()
    {
        SetupTool(ToolWith(WebhookMode.PlayerPing));
        DeliveryPayload.PlayerBlock? captured = null;
        _dispatcher.Setup(d => d.Dispatch(It.IsAny<Tool>(), It.IsAny<DeliveryPayload.PlayerBlock>(),
                It.IsAny<Guid?>(), It.IsAny<IReadOnlyList<DeliveryPayload.Change>>(), It.IsAny<bool>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((Tool _, DeliveryPayload.PlayerBlock p, Guid? _, IReadOnlyList<DeliveryPayload.Change> _,
                bool _, bool _, CancellationToken _) => captured = p)
            .Returns(Task.CompletedTask);

        await Saga().Consume(Batch(MixEnum.FiestaEx, 1));

        Assert.Equal("legacy", captured!.ScoringModel);
        Assert.Equal("MURLOC#1", captured.GameTag);
    }

    /// <summary>
    ///     The rule where a mistake leaks a live piugame.com credential. RedactedString masks
    ///     ToString() but its JSON converter round-trips the real value, so "it's redacted" is not
    ///     protection at the persistence boundary — this is.
    /// </summary>
    [Fact]
    public void ASessionModeBodyIsNeverPersisted()
    {
        foreach (var status in Enum.GetValues<DeliveryStatus>())
            Assert.False(WebhookRetention.ShouldPersistBody(WebhookMode.PiuGameSession, status));
    }

    [Fact]
    public void OnlyBodiesThatCanStillBeUsedArePersisted()
    {
        Assert.True(WebhookRetention.ShouldPersistBody(WebhookMode.ScorePush, DeliveryStatus.Pending));
        Assert.True(WebhookRetention.ShouldPersistBody(WebhookMode.ScorePush, DeliveryStatus.Failed));
        Assert.True(WebhookRetention.ShouldPersistBody(WebhookMode.ScorePush, DeliveryStatus.Abandoned));
        // A success nobody will replay does not need to exist.
        Assert.False(WebhookRetention.ShouldPersistBody(WebhookMode.ScorePush, DeliveryStatus.Succeeded));
    }

    [Fact]
    public void RetryBacksOffAndThenStops()
    {
        Assert.NotNull(WebhookRetry.NextAttemptAfter(1, Now));
        Assert.True(WebhookRetry.NextAttemptAfter(2, Now) > WebhookRetry.NextAttemptAfter(1, Now));
        Assert.Null(WebhookRetry.NextAttemptAfter(WebhookRetry.MaxAttempts, Now));
    }
}
