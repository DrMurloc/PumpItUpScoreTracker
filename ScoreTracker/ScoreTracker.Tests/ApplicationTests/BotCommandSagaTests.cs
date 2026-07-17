using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Moq;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BotCommandSagaTests
{
    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<IDiscordFeedSubscriptionRepository> _feeds = new();
    private readonly Mock<IMediator> _mediator = new();

    private BotCommandSaga Saga() => new(_bot.Object, _communities.Object, _feeds.Object, _mediator.Object);

    private static HandleBotInteractionCommand Invoke(string[] path, Dictionary<string, string> options,
        bool canManage = false) =>
        new(new BotInteraction(path, options, ChannelId: 100, GuildId: 200, UserId: 300,
            UserDisplayName: "Tester", InvokerCanManageChannels: canManage));

    [Fact]
    public async Task CalcReturnsAScoreBreakdownCarryingGradeAndPlateTokens()
    {
        var reply = await Saga().Handle(Invoke(new[] { "calc" }, new Dictionary<string, string>
        {
            ["perfects"] = "950", ["greats"] = "40", ["goods"] = "5",
            ["bads"] = "3", ["misses"] = "2", ["combo"] = "900"
        }), CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.Contains("#LETTERGRADE|", reply.Text);
        Assert.Contains("#PLATE|", reply.Text);
        Assert.Contains("Lost to Greats", reply.Text);
    }

    [Fact]
    public async Task CalcRejectsAnInvalidConfiguration()
    {
        var reply = await Saga().Handle(Invoke(new[] { "calc" }, new Dictionary<string, string>
        {
            ["perfects"] = "10", ["greats"] = "0", ["goods"] = "0",
            ["bads"] = "0", ["misses"] = "0", ["combo"] = "999"
        }), CancellationToken.None);

        Assert.Contains("invalid", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisteringAFeedWithoutManageChannelsIsDenied()
    {
        var reply = await Saga().Handle(Invoke(new[] { "register", "weekly" },
            new Dictionary<string, string> { ["mix"] = "Phoenix2" }), CancellationToken.None);

        Assert.Contains("Manage Channels", reply.Text);
        _feeds.Verify(f => f.Register(It.IsAny<ulong>(), It.IsAny<DiscordFeedKind>(), It.IsAny<MixEnum>(),
            It.IsAny<ulong?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisteringAFeedTheBotCannotPostToFailsUpFront()
    {
        _bot.Setup(b => b.CanPostToChannel(It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var reply = await Saga().Handle(Invoke(new[] { "register", "weekly" },
            new Dictionary<string, string> { ["mix"] = "Phoenix2" }, canManage: true), CancellationToken.None);

        Assert.Contains("permission", reply.Text, StringComparison.OrdinalIgnoreCase);
        _feeds.Verify(f => f.Register(It.IsAny<ulong>(), It.IsAny<DiscordFeedKind>(), It.IsAny<MixEnum>(),
            It.IsAny<ulong?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisteringTheWeeklyFeedPersistsTheSubscriptionForTheChosenMix()
    {
        _bot.Setup(b => b.CanPostToChannel(It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var reply = await Saga().Handle(Invoke(new[] { "register", "weekly" },
            new Dictionary<string, string> { ["mix"] = "Phoenix2" }, canManage: true), CancellationToken.None);

        Assert.Contains("Weekly Charts", reply.Text);
        Assert.Contains("Phoenix 2", reply.Text);
        _feeds.Verify(f => f.Register(100, DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2, 300,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisteringACommunityDispatchesTheAddChannelCommand()
    {
        _bot.Setup(b => b.CanPostToChannel(It.IsAny<ulong>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var reply = await Saga().Handle(Invoke(new[] { "register", "community" },
            new Dictionary<string, string> { ["name"] = "SoCal Pump" }, canManage: true), CancellationToken.None);

        Assert.Contains("registered", reply.Text, StringComparison.OrdinalIgnoreCase);
        _mediator.Verify(m => m.Send(It.Is<AddDiscordChannelToCommunityCommand>(
            c => c.ChannelId == 100 && (string)c.CommunityName! == "SoCal Pump"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnregisteringAFeedRemovesTheSubscription()
    {
        _feeds.Setup(f => f.Unregister(100, DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var reply = await Saga().Handle(Invoke(new[] { "unregister" },
            new Dictionary<string, string> { ["feed"] = "feed:WeeklyCharts:Phoenix2" }, canManage: true),
            CancellationToken.None);

        Assert.Contains("Removed", reply.Text);
        _feeds.Verify(f => f.Unregister(100, DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FeedsListsNothingWhenTheChannelHasNoRegistrations()
    {
        _feeds.Setup(f => f.GetForChannel(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<DiscordFeedSubscriptionRecord>());
        _communities.Setup(c => c.GetChannelCommunityNames(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ScoreTracker.SharedKernel.ValueTypes.Name>());

        var reply = await Saga().Handle(Invoke(new[] { "feeds" }, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.Contains("isn't registered", reply.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FeedsRendersACardListingEachRegistration()
    {
        _feeds.Setup(f => f.GetForChannel(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DiscordFeedSubscriptionRecord(100, DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2),
                new DiscordFeedSubscriptionRecord(100, DiscordFeedKind.DailyStep, MixEnum.Phoenix2)
            });
        _communities.Setup(c => c.GetChannelCommunityNames(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ScoreTracker.SharedKernel.ValueTypes.Name.From("SoCal Pump") });

        var reply = await Saga().Handle(Invoke(new[] { "feeds" }, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.NotNull(reply.Card);
    }

    [Fact]
    public async Task AutocompleteReturnsEmptyForAnUnhandledOption()
    {
        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "calc" }, "perfects", "9",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Empty(choices);
    }
}
