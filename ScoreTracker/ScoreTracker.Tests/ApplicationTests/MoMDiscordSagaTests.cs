using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MediatR;
using Moq;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Application;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Events;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

/// <summary>
///     The MoM Discord card (march-of-murlocs.md §11.7): one card per published session to
///     the mix's subscribed channels, composed per registered language; the biggest five
///     rank by points, never raw score; the context line carries placement; and a session
///     deleted between publish and delivery sends nothing.
/// </summary>
public sealed class MoMDiscordSagaTests
{
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid BoardId = Guid.NewGuid();
    private static readonly MoMSeasonRef Season = new(Guid.NewGuid(), "Winter 2025", 2025, 1);

    [Fact]
    public async Task SendsOneCardPerLanguageWithPointsRankedRowsAndPlacement()
    {
        // Gargoyle: modest score, the session's biggest points haul. Slam: cleaner score,
        // fewer points. Ranked by points Gargoyle leads; ranked by score it would not.
        var gargoyle = new ChartBuilder().WithSongName("Gargoyle").WithLevel(25)
            .WithType(ChartType.Double).Build();
        var slam = new ChartBuilder().WithSongName("Slam").WithLevel(24)
            .WithType(ChartType.Double).Build();
        var context = new Context()
            .WithSession(View(place: 1,
                Row(0, gargoyle.Id, 844710, 3207),
                Row(1, slam.Id, 976489, 1528)))
            .WithCharts(gargoyle, slam)
            .WithChannels(new DiscordFeedChannel(11, null), new DiscordFeedChannel(12, null),
                new DiscordFeedChannel(21, "ko"));

        await context.Consume();

        // English channels batch together; the Korean one gets its own composition.
        context.Bot.Verify(b => b.SendRichMessages(
            It.Is<IEnumerable<RichBotMessage>>(cards => CardIsRight(cards.Single())),
            It.Is<IEnumerable<ulong>>(ids => ids.SequenceEqual(new ulong[] { 11, 12 })),
            It.IsAny<CancellationToken>()), Times.Once);
        context.Bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.Is<IEnumerable<ulong>>(ids => ids.SequenceEqual(new ulong[] { 21 })),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoSubscribedChannelsSendsNothing()
    {
        var context = new Context().WithSession(View(place: 1));

        await context.Consume();

        context.Bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASessionDeletedBeforeDeliverySendsNothing()
    {
        var context = new Context()
            .WithChannels(new DiscordFeedChannel(11, null));
        // GetMoMSessionQuery answers null: deleted (or somehow a draft again) — no card.

        await context.Consume();

        context.Bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static bool CardIsRight(RichBotMessage card)
    {
        var text = card.Header!.Markdown + "\n" +
                   string.Join("\n", card.Blocks.OfType<RichBotText>().Select(b => b.Markdown));
        // The header names the player and the total; the context line carries the board —
        // a number without a chart type says nothing (D15) — and the placement.
        Assert.Contains("김재현", text);
        Assert.Contains("4,735", text);
        Assert.Contains("Winter 2025", text);
        Assert.Contains("Doubles", text);
        Assert.Contains("#1 of 11", text);
        // Biggest five by POINTS: Gargoyle's 3,207 leads Slam's cleaner 1,528.
        var gargoyleAt = text.IndexOf("Gargoyle", StringComparison.Ordinal);
        var slamAt = text.IndexOf("Slam", StringComparison.Ordinal);
        Assert.True(gargoyleAt >= 0 && slamAt > gargoyleAt,
            "biggest five must rank by points, Gargoyle first");
        Assert.Single(card.Links);
        Assert.EndsWith($"/MarchOfMurlocs/Session/{SessionId}", card.Links[0].Url.ToString());
        return true;
    }

    private static MoMSessionView View(int? place, params MoMSessionChartRow[] charts)
    {
        return new MoMSessionView(SessionId, BoardId, Season, MixEnum.Phoenix, ChartType.Double,
            Guid.NewGuid(), "김재현", DateTimeOffset.UtcNow, charts.Sum(c => c.SessionScore),
            charts.Length, TimeSpan.FromMinutes(22), 24.22, 11.2, 21, 26, null, place,
            TimeSpan.FromMinutes(105), false, charts);
    }

    private static MoMSessionChartRow Row(int ordinal, Guid chartId, int score, int points)
    {
        return new MoMSessionChartRow(ordinal, chartId, score, PhoenixPlate.RoughGame, false,
            points, 0, null, 25.5);
    }

    private sealed class Context
    {
        private readonly Mock<IChartRepository> _charts = new();
        private readonly Mock<ConsumeContext<MoMSessionPublishedEvent>> _context = new();
        private readonly Mock<IDiscordFeedReader> _feeds = new();
        private readonly Mock<ILocalizedTextAccessor> _localizer = new();
        private readonly Mock<IMediator> _mediator = new();
        private readonly Mock<IUserReader> _users = new();

        public Context()
        {
            _context.Setup(c => c.Message)
                .Returns(new MoMSessionPublishedEvent(SessionId, BoardId, Guid.NewGuid()));
            _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.MarchOfMurlocs,
                    MixEnum.Phoenix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<DiscordFeedChannel>());
            _mediator.Setup(m => m.Send(It.IsAny<GetMoMBoardQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MoMBoardView(BoardId, Season, MixEnum.Phoenix, ChartType.Double,
                    Enumerable.Range(1, 11).Select(i => new MoMBoardRow(i, Guid.NewGuid(),
                        Guid.NewGuid(), $"p{i}", null, null, 60000 - i, 30, 24, 11, 21, 26,
                        TimeSpan.FromMinutes(20), DateTimeOffset.UtcNow, null)).ToArray()));
            _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>()))
                .Returns((string? _, string key) => key);
            _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
                .Returns((string? _, string key, object[] args) => string.Format(key, args));
            _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null,
                    It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<Chart>());
        }

        public Mock<IBotClient> Bot { get; } = new();

        public Context WithSession(MoMSessionView view)
        {
            _mediator.Setup(m => m.Send(It.IsAny<GetMoMSessionQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(view);
            return this;
        }

        public Context WithCharts(params Chart[] charts)
        {
            _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null,
                    It.IsAny<IEnumerable<Guid>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(charts);
            return this;
        }

        public Context WithChannels(params DiscordFeedChannel[] channels)
        {
            _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.MarchOfMurlocs,
                    MixEnum.Phoenix, It.IsAny<CancellationToken>()))
                .ReturnsAsync(channels);
            return this;
        }

        public Task Consume()
        {
            return new MoMDiscordSaga(Bot.Object, _feeds.Object, _mediator.Object, _charts.Object,
                _users.Object, _localizer.Object).Consume(_context.Object);
        }
    }
}
