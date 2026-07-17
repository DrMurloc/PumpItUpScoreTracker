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
using ScoreTracker.OfficialMirror.Application;
using ScoreTracker.OfficialMirror.Contracts;
using ScoreTracker.OfficialMirror.Contracts.Events;
using ScoreTracker.OfficialMirror.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class OfficialDigestFeedSagaTests
{
    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IDiscordFeedReader> _feeds = new();
    private readonly Mock<IMediator> _mediator = new();
    private List<RichBotMessage> _sent = new();

    public OfficialDigestFeedSagaTests()
    {
        _bot.Setup(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
                It.IsAny<IEnumerable<ulong>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<RichBotMessage>, IEnumerable<ulong>, CancellationToken>((m, _, _) =>
                _sent = m.ToList())
            .Returns(Task.CompletedTask);
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Chart>());
    }

    private OfficialDigestFeedSaga Saga() =>
        new(_bot.Object, _charts.Object, _feeds.Object, _mediator.Object);

    private static ConsumeContext<OfficialSnapshotSealedEvent> Context(OfficialSnapshotSealedEvent message)
    {
        var ctx = new Mock<ConsumeContext<OfficialSnapshotSealedEvent>>();
        ctx.SetupGet(c => c.Message).Returns(message);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static OfficialPlayerRecord Player(string name) => new(1, name, null, null);

    [Fact]
    public async Task BaselineSealSkipsTheDigestEntirely()
    {
        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, IsBaseline: true)));

        Assert.Empty(_sent);
        _feeds.Verify(f => f.GetSubscribedChannels(It.IsAny<string>(), It.IsAny<MixEnum>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SkipsWhenNoChannelSubscribes()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<ulong>());

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        Assert.Empty(_sent);
        _mediator.Verify(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PostsADigestWithMoversAndCutlines()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new ulong[] { 123 });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(-7),
                new[] { new OfficialMoverRecord(Player("HYSTERIA"), 58, 41, 9120.45m) },
                Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(),
                Array.Empty<OfficialNewNumberOneRecord>()));
        _mediator.Setup(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatItTakesRecord(DateTimeOffset.UnixEpoch, true, 1000, null,
                Array.Empty<CutlineTierRecord>(),
                new[] { new BoardCutlineRecord("All", 7842.10m, 34.55m, true) },
                Array.Empty<CutlineHistoryPointRecord>()));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        Assert.Single(_sent);
        var text = string.Join("\n", _sent[0].Blocks.OfType<RichBotText>().Select(t => t.Markdown));
        Assert.Contains("HYSTERIA", text);
        Assert.Contains("What it takes", text);
        Assert.Contains("▲", text);
    }
}
