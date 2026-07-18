using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.ChartIntelligence.Contracts;
using ScoreTracker.ChartIntelligence.Contracts.Queries;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Commands;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Identity.Contracts.Queries;
using ScoreTracker.PlayerProgress.Contracts.Queries;
using ScoreTracker.Randomizer.Contracts;
using ScoreTracker.Randomizer.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class BotCommandSagaTests
{
    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<ICurrentUserAccessor> _currentUser = new();
    private readonly Mock<IDiscordFeedSubscriptionRepository> _feeds = new();
    private readonly Mock<IMediator> _mediator = new();

    private BotCommandSaga Saga() =>
        new(_bot.Object, _communities.Object, _feeds.Object, _mediator.Object, _currentUser.Object);

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
        _communities.Setup(c => c.GetChannelCommunities(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChannelCommunityInfo>());

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
        _communities.Setup(c => c.GetChannelCommunities(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChannelCommunityInfo(Name.From("SoCal Pump"), false) });

        var reply = await Saga().Handle(Invoke(new[] { "feeds" }, new Dictionary<string, string>()),
            CancellationToken.None);

        Assert.NotNull(reply.Card);
    }

    [Fact]
    public async Task UnregisteringACommunityDispatchesTheRemoveCommand()
    {
        var reply = await Saga().Handle(Invoke(new[] { "unregister" },
            new Dictionary<string, string> { ["feed"] = "community:SoCal Pump" }, canManage: true),
            CancellationToken.None);

        Assert.Contains("Removed", reply.Text);
        _mediator.Verify(m => m.Send(It.Is<RemoveDiscordChannelFromCommunityCommand>(
            c => (string)c.CommunityName == "SoCal Pump" && c.ChannelId == 100), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommunityNameAutocompleteFiltersPublicCommunities()
    {
        _communities.Setup(c => c.GetPublicCommunities(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new CommunityOverviewRecord(Name.From("SoCal Pump"), CommunityPrivacyType.Public, 5, false),
                new CommunityOverviewRecord(Name.From("NorCal"), CommunityPrivacyType.Public, 3, false)
            });

        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "register", "community" }, "name", "so",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Single(choices);
        Assert.Equal("SoCal Pump", choices[0].Value);
    }

    [Fact]
    public async Task FeedAutocompleteListsThisChannelsRegistrations()
    {
        _feeds.Setup(f => f.GetForChannel(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new DiscordFeedSubscriptionRecord(100, DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2) });
        _communities.Setup(c => c.GetChannelCommunities(100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChannelCommunityInfo(Name.From("SoCal Pump"), false) });

        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "unregister" }, "feed", "",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 100, GuildId: 3)), CancellationToken.None);

        Assert.Equal(2, choices.Count);
        Assert.Contains(choices, c => c.Value == "feed:WeeklyCharts:Phoenix2");
        Assert.Contains(choices, c => c.Value == "community:SoCal Pump");
    }

    [Fact]
    public async Task PresetAutocompleteListsSavedSettingsForALinkedUser()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().Build());
        _mediator.Setup(m => m.Send(It.IsAny<GetRandomSettingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new SavedRandomizerSettings(Name.From("Bracket Warmup"), new RandomSettings(), MixEnum.Phoenix2)
            }.AsEnumerable());

        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "random" }, "preset", "brac",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Single(choices);
        Assert.Equal("Bracket Warmup", choices[0].Value);
    }

    [Fact]
    public async Task PresetAutocompleteReturnsNothingForAnUnlinkedUser()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "random" }, "preset", "b",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Empty(choices);
    }

    [Fact]
    public async Task AutocompleteReturnsEmptyForAnUnhandledOption()
    {
        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "calc" }, "perfects", "9",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Empty(choices);
    }

    private static Chart[] SampleCharts() => new Chart[]
    {
        new ChartBuilder().WithSongName("Ugly Dee").WithArtist("Banya").WithType(ChartType.Single)
            .WithLevel(20).WithMix(MixEnum.Phoenix2),
        new ChartBuilder().WithSongName("Ugly Dee").WithArtist("Banya").WithType(ChartType.Double)
            .WithLevel(21).WithMix(MixEnum.Phoenix2),
        new ChartBuilder().WithSongName("Bad Apple").WithArtist("Masayoshi").WithType(ChartType.Single)
            .WithLevel(17).WithMix(MixEnum.Phoenix2)
    };

    [Fact]
    public async Task ChartLookupRendersACardWithEveryDifficultyOfTheMatchedSong()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleCharts());

        var reply = await Saga().Handle(Invoke(new[] { "chart" },
            new Dictionary<string, string> { ["song"] = "Ugly Dee", ["mix"] = "Phoenix2" }), CancellationToken.None);

        Assert.NotNull(reply.Card);
        Assert.Contains("Ugly Dee", reply.Card!.Header!.Markdown);
        var rows = reply.Card.Blocks.OfType<RichBotText>().First().Markdown;
        Assert.Contains("#DIFFICULTY|S20#", rows);
        Assert.Contains("#DIFFICULTY|D21#", rows);
        Assert.Contains("/Chart/", rows);
        Assert.DoesNotContain("Bad Apple", rows);
    }

    [Fact]
    public async Task ChartLookupRepliesNotFoundWhenNoSongMatches()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleCharts());

        var reply = await Saga().Handle(Invoke(new[] { "chart" },
            new Dictionary<string, string> { ["song"] = "Nonexistent" }), CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.Contains("No chart found", reply.Text);
    }

    private static string CardText(RichBotMessage card) =>
        string.Join("\n", new[] { card.Header?.Markdown, card.Footer }
            .Concat(card.Blocks.Select(b => b switch
            {
                RichBotText t => t.Markdown,
                RichBotSection s => s.Markdown,
                _ => string.Empty
            }))
            .Where(x => x != null));

    [Fact]
    public async Task SongAutocompleteReturnsOneEntryPerMatchingChartKeyedByChartId()
    {
        var charts = SampleCharts();
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(charts);

        var choices = await Saga().Handle(new GetBotAutocompleteQuery(
            new BotAutocompleteRequest(new[] { "chart" }, "song", "ug",
                new Dictionary<string, string>(), UserId: 1, ChannelId: 2, GuildId: 3)), CancellationToken.None);

        Assert.Equal(2, choices.Count); // Ugly Dee S20 + Ugly Dee D21, not Bad Apple
        Assert.All(choices, c => Assert.Contains("Ugly Dee", c.Name));
        Assert.Contains(choices, c => c.Value == charts[0].Id.ToString());
    }

    [Fact]
    public async Task ChartDetailCardShowsTheBreakdownSkillsAndSimilarCharts()
    {
        var target = new ChartBuilder().WithId(new Guid("00000000-0000-0000-0000-0000000000d1"))
            .WithSongName("District 1").WithArtist("SHK").WithType(ChartType.Single).WithLevel(21)
            .WithMix(MixEnum.Phoenix2).Build();
        var neighbor = new ChartBuilder().WithId(new Guid("00000000-0000-0000-0000-0000000000d2"))
            .WithSongName("Vacuum").WithType(ChartType.Double).WithLevel(19).WithMix(MixEnum.Phoenix2).Build();
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { target, neighbor });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartScoringLevelsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, double> { [target.Id] = 21.4 });
        _mediator.Setup(m => m.Send(It.IsAny<GetTierListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ScoreTracker.Domain.Records.SongTierListEntry(Name.From("Pass Count"), target.Id,
                    TierListCategory.Medium, 0)
            }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartStepAnalysisQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChartStepAnalysisRecord(new[] { "run", "twist_90" },
                new Dictionary<string, decimal>(), null, null, null, null, null));
        _mediator.Setup(m => m.Send(It.IsAny<GetSimilarChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartSimilarityRecord(neighbor.Id, 0.82, 0.9, 0.6, Array.Empty<ChartSharedBadgeRecord>())
            });

        var reply = await Saga().Handle(Invoke(new[] { "chart" },
            new Dictionary<string, string> { ["song"] = target.Id.ToString(), ["mix"] = "Phoenix2" }),
            CancellationToken.None);

        Assert.NotNull(reply.Card);
        var text = CardText(reply.Card!);
        Assert.Contains("District 1", text);
        Assert.Contains("Scoring level", text);
        Assert.Contains("Pass **Medium**", text);
        Assert.Contains("Run", text); // prettified from the raw "run" step-analysis skill
        Assert.Contains("Twist 90", text); // "twist_90" → "Twist 90"
        Assert.Contains("Similar charts", text);
        Assert.Contains("Vacuum", text);
    }

    [Fact]
    public async Task RandomDrawRendersACardOfTheDrawnCharts()
    {
        _mediator.Setup(m => m.Send(It.IsAny<ScoreTracker.Randomizer.Contracts.Queries.DrawRandomChartsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleCharts().Take(2).ToArray());

        var reply = await Saga().Handle(Invoke(new[] { "random" },
            new Dictionary<string, string> { ["count"] = "2", ["type"] = "Single", ["mix"] = "Phoenix2" }),
            CancellationToken.None);

        Assert.NotNull(reply.Card);
        Assert.Contains("Drew 2", reply.Card!.Header!.Markdown);
    }

    [Fact]
    public async Task RandomPresetNudgesAnUnlinkedUserToConnectDiscord()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var reply = await Saga().Handle(Invoke(new[] { "random" },
            new Dictionary<string, string> { ["preset"] = "Bracket Warmup" }), CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.Contains("Link your Discord account", reply.Text);
    }

    [Fact]
    public async Task SuggestNudgesAnUnlinkedUser()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var reply = await Saga().Handle(Invoke(new[] { "suggest" },
            new Dictionary<string, string> { ["goal"] = "TitleHunt" }), CancellationToken.None);

        Assert.Null(reply.Card);
        Assert.Contains("Link your Discord account", reply.Text);
    }

    [Fact]
    public async Task SuggestRendersACardOfRecommendationsForALinkedUser()
    {
        var chartId = new Guid("00000000-0000-0000-0000-0000000000aa");
        var chart = new ChartBuilder().WithId(chartId).WithSongName("District 1").WithArtist("SHK")
            .WithType(ChartType.Single).WithLevel(21).WithMix(MixEnum.Phoenix2).Build();
        _mediator.Setup(m => m.Send(It.IsAny<GetUserByExternalLoginQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserBuilder().Build());
        _mediator.Setup(m => m.Send(It.IsAny<GetRecommendedChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChartRecommendation(Name.From("Skill Title Charts"), chartId, "A strong pick") });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart });

        var reply = await Saga().Handle(Invoke(new[] { "suggest" },
            new Dictionary<string, string> { ["goal"] = "TitleHunt", ["mix"] = "Phoenix2" }), CancellationToken.None);

        Assert.NotNull(reply.Card);
        Assert.Contains("Suggested for you", reply.Card!.Header!.Markdown);
        Assert.Contains("District 1", reply.Card.Blocks.OfType<RichBotSection>().Single().Markdown);
    }
}
