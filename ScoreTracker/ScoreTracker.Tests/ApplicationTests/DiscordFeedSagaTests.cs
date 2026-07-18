using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Catalog.Contracts.Queries;
using ScoreTracker.Communities.Application;
using ScoreTracker.Communities.Contracts;
using ScoreTracker.Communities.Contracts.Queries;
using ScoreTracker.Communities.Domain;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using ScoreTracker.WeeklyChallenge.Contracts;
using ScoreTracker.WeeklyChallenge.Contracts.Events;
using ScoreTracker.WeeklyChallenge.Contracts.Queries;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class DiscordFeedSagaTests
{
    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<ICommunityRepository> _communities = new();
    private readonly Mock<IDiscordFeedSubscriptionRepository> _feeds = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUserReader> _users = new();
    private List<RichBotMessage> _sent = new();

    public DiscordFeedSagaTests()
    {
        // Defaults registered here (not in Saga()) so a test's more specific setup, run in the
        // test body afterwards, wins under Moq's last-matching-setup rule.
        _bot.Setup(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
                It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RichBotMessage>, IEnumerable<ulong>, CancellationToken>((m, _, _) =>
                _sent = m.ToList())
            .Returns(Task.CompletedTask);
        _communities.Setup(c => c.GetChannelCommunities(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ChannelCommunityInfo>());
    }

    private DiscordFeedSaga Saga() =>
        new(_bot.Object, _mediator.Object, _users.Object, _feeds.Object, _communities.Object);

    private static ConsumeContext<T> Context<T>(T message) where T : class
    {
        var ctx = new Mock<ConsumeContext<T>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static string AllText(RichBotMessage message) =>
        string.Join("\n", new[] { message.Header?.Markdown, message.Footer }
            .Concat(message.Blocks.Select(b => b switch
            {
                RichBotText t => t.Markdown,
                RichBotSection s => s.Markdown,
                _ => string.Empty
            }))
            .Where(x => x != null));

    [Fact]
    public async Task WeeklyFeedPostsAResultCardPerChartPlusTheLineup()
    {
        var chart = new ChartBuilder().WithSongName("District 1").WithType(ChartType.Double).WithLevel(24)
            .WithMix(MixEnum.Phoenix2).Build();
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKind.WeeklyCharts, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        _mediator.Setup(m => m.Send(It.IsAny<GetPastWeeklyDatesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { DateTimeOffset.UnixEpoch }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetPastWeeklyEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WeeklyTournamentEntry(Guid.NewGuid(), chart.Id, 990000, PhoenixPlate.UltimateGame, false, null, 24),
                new WeeklyTournamentEntry(Guid.NewGuid(), chart.Id, 980000, PhoenixPlate.SuperbGame, false, null, 24)
            }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chart }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WeeklyTournamentChart(chart.Id, DateTimeOffset.UnixEpoch) }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartVideosQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new ChartVideoInformation(chart.Id, new Uri("https://youtu.be/xyz"), Name.From("A Channel"))
            }.AsEnumerable());
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        await Saga().Consume(Context(new WeeklyChartsRotatedEvent(MixEnum.Phoenix2)));

        Assert.Equal(2, _sent.Count); // one result card + the lineup card
        Assert.Contains("final board", AllText(_sent[0]));
        Assert.Contains("This week's charts", AllText(_sent[1]));
        Assert.Contains("[Video]", AllText(_sent[1])); // lineup carries the video link
    }

    [Fact]
    public async Task WeeklyFeedSkipsEntirelyWhenNoChannelSubscribes()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKind.WeeklyCharts, It.IsAny<MixEnum>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiscordFeedChannel>());

        await Saga().Consume(Context(new WeeklyChartsRotatedEvent(MixEnum.Phoenix2)));

        Assert.Empty(_sent);
        _mediator.Verify(m => m.Send(It.IsAny<GetPastWeeklyDatesQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DailyFeedPostsYesterdayResultsAndTodaysChart()
    {
        var yesterday = new ChartBuilder().WithSongName("Trashy Innocence").WithType(ChartType.Single).WithLevel(19)
            .WithMix(MixEnum.Phoenix2).Build();
        var today = new ChartBuilder().WithSongName("Bee").WithType(ChartType.Single).WithLevel(7)
            .WithMix(MixEnum.Phoenix2).Build();
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKind.DailyStep, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { yesterday, today }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetDailyStepQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyStepBoard(today.Id, DateTimeOffset.UnixEpoch, true, DateTimeOffset.UnixEpoch));
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<User>());

        var evt = new DailyStepRotatedEvent(MixEnum.Phoenix2, yesterday.Id, DateTimeOffset.UnixEpoch, false,
            new[] { new DailyStepResult(1, Guid.NewGuid(), 995000, PhoenixPlate.PerfectGame, false) });
        await Saga().Consume(Context(evt));

        Assert.Single(_sent);
        var text = AllText(_sent[0]);
        Assert.Contains("Trashy Innocence", text);
        Assert.Contains("Bee", text);
        Assert.Contains("Limbo Day", text);
    }

    private void SetupDailyGlowScenario(Guid memberId, Chart today, bool isRegional)
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKind.DailyStep, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        _communities.Setup(c => c.GetChannelCommunities(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new ChannelCommunityInfo(Name.From("Arrow Eclipse"), isRegional) });
        _mediator.Setup(m => m.Send(It.IsAny<GetCommunityMembersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { memberId }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetChartsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { today }.AsEnumerable());
        _mediator.Setup(m => m.Send(It.IsAny<GetDailyStepQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyStepBoard(today.Id, DateTimeOffset.UnixEpoch, false, DateTimeOffset.UnixEpoch));
        _users.Setup(u => u.GetUsers(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new UserBuilder().WithId(memberId).WithName("MELON").Build() }.AsEnumerable());
    }

    [Fact]
    public async Task DailyFeedLabelsCommunityMembersWithTheirCommunityName()
    {
        var memberId = Guid.NewGuid();
        var today = new ChartBuilder().WithSongName("Bee").WithType(ChartType.Single).WithLevel(16)
            .WithMix(MixEnum.Phoenix2).Build();
        SetupDailyGlowScenario(memberId, today, isRegional: false);

        await Saga().Consume(Context(new DailyStepRotatedEvent(MixEnum.Phoenix2, today.Id, DateTimeOffset.UnixEpoch,
            false, new[] { new DailyStepResult(1, memberId, 990000, PhoenixPlate.ExtremeGame, false) })));

        var text = AllText(_sent[0]);
        Assert.Contains("(Arrow Eclipse)", text);
        Assert.DoesNotContain("🟢", text);
    }

    [Fact]
    public async Task DailyFeedDoesNotLabelRegionalCommunityMembers()
    {
        var memberId = Guid.NewGuid();
        var today = new ChartBuilder().WithSongName("Bee").WithType(ChartType.Single).WithLevel(16)
            .WithMix(MixEnum.Phoenix2).Build();
        SetupDailyGlowScenario(memberId, today, isRegional: true);

        await Saga().Consume(Context(new DailyStepRotatedEvent(MixEnum.Phoenix2, today.Id, DateTimeOffset.UnixEpoch,
            false, new[] { new DailyStepResult(1, memberId, 990000, PhoenixPlate.ExtremeGame, false) })));

        Assert.DoesNotContain("(Arrow Eclipse)", AllText(_sent[0]));
    }
}
