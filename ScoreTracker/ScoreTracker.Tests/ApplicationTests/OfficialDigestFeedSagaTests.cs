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
using ScoreTracker.Tests.TestData;
using Xunit;

namespace ScoreTracker.Tests.ApplicationTests;

public sealed class OfficialDigestFeedSagaTests
{
    private readonly Mock<IBotClient> _bot = new();
    private readonly Mock<IChartRepository> _charts = new();
    private readonly Mock<IDiscordFeedReader> _feeds = new();
    private readonly Mock<ILocalizedTextAccessor> _localizer = new();
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
        // Identity localizer: English keys back regardless of culture, so text assertions
        // stay culture-independent.
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>()))
            .Returns((string? _, string key) => key);
        _localizer.Setup(l => l.Get(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string? _, string key, object[] args) => string.Format(key, args));
    }

    private OfficialDigestFeedSaga Saga() =>
        new(_bot.Object, _charts.Object, _feeds.Object, _mediator.Object, _localizer.Object);

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
            It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<DiscordFeedChannel>());

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        Assert.Empty(_sent);
        _mediator.Verify(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChannelsGroupByLanguageAndEachGroupGetsItsOwnComposition()
    {
        // Two Korean channels share one composed card and one send; the English channel
        // gets its own.
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new DiscordFeedChannel(1, "ko-KR"), new DiscordFeedChannel(2, "ko-KR"),
                new DiscordFeedChannel(3, null)
            });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(-7),
                new[] { new OfficialMoverRecord(Player("HYSTERIA"), 58, 41, 9120.45m) },
                Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(),
                Array.Empty<OfficialNewNumberOneRecord>()));
        _mediator.Setup(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatItTakesRecord?)null);
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OfficialRankingsRecord?)null);

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.Is<IEnumerable<ulong>>(ids => ids.OrderBy(i => i).SequenceEqual(new ulong[] { 1, 2 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.Is<IEnumerable<ulong>>(ids => ids.Count() == 1 && ids.First() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _localizer.Verify(l => l.Get("ko-KR", "PUMBILITY movers"), Times.Once);
        _localizer.Verify(l => l.Get(null, "PUMBILITY movers"), Times.Once);
    }

    [Fact]
    public async Task PostsADigestWithTopTenMoversAndDifficultyCutlines()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        var paradoxx = new ChartBuilder().WithSongName("Paradoxx").WithType(ChartType.Single).WithLevel(26)
            .WithMix(MixEnum.Phoenix2).Build();
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { paradoxx });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(-7),
                new[] { new OfficialMoverRecord(Player("HYSTERIA"), 58, 41, 9120.45m) },
                Array.Empty<OfficialBoardsClimbedRecord>(),
                new[] { new OfficialGradeFirstRecord(Player("ESI"), paradoxx.Id, "S", 26, "SSS+", 995120, false) },
                Array.Empty<OfficialNewNumberOneRecord>()));
        _mediator.Setup(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatItTakesRecord(DateTimeOffset.UnixEpoch, true, 1000,
                new CutlineTierRecord(1000, 7842.10m, 34.55m, 20, 18, 17, 16),
                Array.Empty<CutlineTierRecord>(),
                Array.Empty<BoardCutlineRecord>(),
                Array.Empty<CutlineHistoryPointRecord>()));
        _mediator.Setup(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OfficialRankingsRecord(DateTimeOffset.UnixEpoch, true,
                new[] { new OfficialRankingRecord(1, 3, Player("JEWEL"), 9999m, 50, null) }));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        Assert.Single(_sent);
        var text = string.Join("\n", _sent[0].Blocks.OfType<RichBotText>().Select(t => t.Markdown));
        Assert.Contains("PUMBILITY top 10", text);
        Assert.Contains("JEWEL", text);
        Assert.Contains("↑2", text); // moved from rank 3 to rank 1
        Assert.Contains("50× AAA at Lv.20", text);
        Assert.Contains("50× SSS at Lv.16", text);
        Assert.Contains(_sent[0].Blocks, b => b is RichBotDivider); // sections are fenced for readability
        // World-first lines: bubble token, song, "World First", the grade as its emoji, the
        // player linked to their board profile — no raw score on the line.
        Assert.Contains("#DIFFICULTY|S26# **Paradoxx** — World First #LETTERGRADE|SSSPlus# — " +
                        "[ESI](https://piuscores.arroweclip.se/OfficialLeaderboards/Players?player=ESI)", text);
        Assert.DoesNotContain("995,120", text);
    }
}
