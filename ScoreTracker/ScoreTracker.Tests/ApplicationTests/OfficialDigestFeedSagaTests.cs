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
using ScoreTracker.SharedKernel.ValueTypes;
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

    // Board tags carry a discriminator; the card prints the human half and links on the full tag.
    private static OfficialPlayerRecord Player(string name) => new(1, name + "#1489", null, null);

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
                Array.Empty<OfficialMoverRecord>(), Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(), Array.Empty<OfficialNewNumberOneRecord>(),
                new WeeklyPulseRecord(23273, 3214, 1857, 375),
                new[] { new OfficialGainerRecord(Player("HYSTERIA"), 9120.45m, 9500m, 58, 41) }));
        _mediator.Setup(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WhatItTakesRecord?)null);

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.Is<IEnumerable<ulong>>(ids => ids.OrderBy(i => i).SequenceEqual(new ulong[] { 1, 2 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _bot.Verify(b => b.SendRichMessages(It.IsAny<IEnumerable<RichBotMessage>>(),
            It.Is<IEnumerable<ulong>>(ids => ids.Count() == 1 && ids.First() == 3),
            It.IsAny<CancellationToken>()), Times.Once);
        _localizer.Verify(l => l.Get("ko-KR", "Biggest PUMBILITY gain"), Times.Once);
        _localizer.Verify(l => l.Get(null, "Biggest PUMBILITY gain"), Times.Once);
    }

    [Fact]
    public async Task LeadsWithTheWeeksPulseAndWearsTheTopFirstsJacket()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        var lower = new ChartBuilder().WithSongName("Digitalis").WithType(ChartType.Double).WithLevel(24)
            .WithSong(new Song(Name.From("Digitalis"), SongType.Arcade,
                new Uri("https://example.invalid/digitalis.png"), TimeSpan.FromMinutes(2),
                Name.From("A"), null)).Build();
        var marquee = new ChartBuilder().WithType(ChartType.Double).WithLevel(27)
            .WithSong(new Song(Name.From("Freedom Dive"), SongType.Arcade,
                new Uri("https://example.invalid/freedomdive.png"), TimeSpan.FromMinutes(2),
                Name.From("B"), null)).Build();
        _charts.Setup(c => c.GetCharts(It.IsAny<MixEnum>(), null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { lower, marquee });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(-7),
                Array.Empty<OfficialMoverRecord>(), Array.Empty<OfficialBoardsClimbedRecord>(),
                new[]
                {
                    // A perfect game on a lower chart must not outrank a lesser grade higher up.
                    new OfficialGradeFirstRecord(Player("FEFEMZ"), lower.Id, "D", 24, "PG", 1000000, false),
                    new OfficialGradeFirstRecord(Player("FRANCO"), marquee.Id, "D", 27, "AAA+", 964378, false)
                },
                Array.Empty<OfficialNewNumberOneRecord>(),
                new WeeklyPulseRecord(23273, 3214, 1857, 375)));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        var card = Assert.Single(_sent);
        // The jacket belongs to the highest-level first, and the hype sentence is its caption.
        Assert.Equal(marquee.Song.ImagePath, card.Header!.Thumbnail);
        Assert.Contains("1,857 players left their mark — and Freedom Dive D27 fell to its first AAA+.",
            card.Header.Markdown);

        var text = string.Join("\n", card.Blocks.OfType<RichBotText>().Select(t => t.Markdown));
        // Entries are the two halves added together; the split rides the subtext below.
        Assert.Contains("**26,487** board entries · **1,857** players active · **375** debuts", text);
        Assert.Contains("-# 23,273 new · 3,214 upscored", text);
    }

    [Fact]
    public async Task PostsADigestWithOneMarqueeNamePerCategory()
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
                new[]
                {
                    new OfficialBoardsClimbedRecord(Player("MECCHAMILE"), 90, 11990, 90),
                    new OfficialBoardsClimbedRecord(Player("BG"), 105, 11946, 105)
                },
                new[] { new OfficialGradeFirstRecord(Player("ESI"), paradoxx.Id, "S", 26, "SSS+", 995120, false) },
                new[] { new OfficialNewNumberOneRecord(Player("ORIU"), paradoxx.Id, 999720, Player("CLUCLE")) },
                new WeeklyPulseRecord(23273, 3214, 1857, 375),
                new[] { new OfficialGainerRecord(Player("RUN"), 15731.46m, 18645.90m, 908, 41) }));
        _mediator.Setup(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WhatItTakesRecord(DateTimeOffset.UnixEpoch, true, 1000,
                new CutlineTierRecord(1000, 7842.10m, 34.55m, 20, 18, 17, 16),
                Array.Empty<CutlineTierRecord>(),
                Array.Empty<BoardCutlineRecord>(),
                Array.Empty<CutlineHistoryPointRecord>()));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        Assert.Single(_sent);
        var text = string.Join("\n", _sent[0].Blocks.OfType<RichBotText>().Select(t => t.Markdown));
        Assert.Contains(_sent[0].Blocks, b => b is RichBotDivider); // sections are fenced for readability

        // The gainer leads on value won, and only the top one appears.
        Assert.Contains("**+2,914.44** to 18,645.90 · #908 → **#41**", text);
        // The climber likewise — one name, with the entered/climbed split intact.
        Assert.Contains("**+11,990 places** across 90 chart boards · 90 new", text);
        Assert.DoesNotContain("BG", text);

        // Standings and the new-#1 sample are gone: the first is not news and the second ran
        // to three figures on a real week, so four of them read as the whole story.
        Assert.DoesNotContain("PUMBILITY top 10", text);
        Assert.DoesNotContain("New #1", text);
        Assert.DoesNotContain("ORIU", text);
        Assert.DoesNotContain("dethroning", text);

        // World-first lines keep their shape: bubble token, song, "World First", the grade as
        // its emoji, the player linked to their board profile — no raw score on the line. The
        // link text is the human half of the tag; the query parameter keeps the whole thing,
        // because that is what /Players resolves on.
        Assert.Contains("#DIFFICULTY|S26# **Paradoxx** — World First #LETTERGRADE|SSSPlus# — " +
                        "[ESI](https://piuscores.arroweclip.se/OfficialLeaderboards/Players?player=ESI%231489)",
            text);
        Assert.DoesNotContain("995,120", text);
    }

    [Fact]
    public async Task DrawsBothFloorsInAAAWithLastWeeksLevelBesideThem()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(-7),
                Array.Empty<OfficialMoverRecord>(), Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(), Array.Empty<OfficialNewNumberOneRecord>(),
                new WeeklyPulseRecord(1, 1, 1, 1), Array.Empty<OfficialGainerRecord>(),
                Array.Empty<OfficialDebutRecord>(),
                // The 2026-08-02 Phoenix 2 sweep's real floors. The stored SS levels (23 and
                // 18) are deliberately absurd here: the card must ignore them and derive AAA.
                new[]
                {
                    new OfficialFloorMarkRecord(100, 18283.13m, 18097.98m, 99, 99),
                    new OfficialFloorMarkRecord(1000, 16489.37m, 15219.28m, 99, 99)
                }));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        var text = string.Join("\n", _sent[0].Blocks.OfType<RichBotText>().Select(t => t.Markdown));
        Assert.Contains("50× AAA on singles", text);
        Assert.DoesNotContain("Lv.99", text); // the stored SS level is never what gets drawn
        // Rank labels are right-aligned so the two rungs line up under a proportional font.
        Assert.Contains("` #100`", text);
        Assert.Contains("`#1000`", text);
        // A rising floor shows the week's climb; both rose that week.
        Assert.Contains("18,283.13 ▲185", text);
        Assert.Contains("16,489.37 ▲1,270", text);
        // A floor that rose without crossing a level shows one level, not an arrow to itself;
        // one that crossed four shows where it came from. (Deliberately not pinning the level
        // numbers: those follow pumbility scoring, which is not this card's business.)
        Assert.Matches(@"` #100` \*\*Lv\.\d+\*\* ·", text);
        Assert.Matches(@"`#1000` Lv\.\d+ → \*\*Lv\.\d+\*\* ·", text);
    }

    [Fact]
    public async Task NeverAsksForTheRankingsBoardOrTheCutlines()
    {
        _feeds.Setup(f => f.GetSubscribedChannels(DiscordFeedKinds.OfficialLeaderboards, MixEnum.Phoenix2,
            It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new DiscordFeedChannel(123, null) });
        _mediator.Setup(m => m.Send(It.IsAny<GetWeeklyHighlightsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WeeklyHighlightsRecord(DateTimeOffset.UnixEpoch, null,
                Array.Empty<OfficialMoverRecord>(), Array.Empty<OfficialBoardsClimbedRecord>(),
                Array.Empty<OfficialGradeFirstRecord>(), Array.Empty<OfficialNewNumberOneRecord>(),
                new WeeklyPulseRecord(1, 1, 1, 1)));

        await Saga().Consume(Context(new OfficialSnapshotSealedEvent(MixEnum.Phoenix2, false)));

        // Both existed only to render blocks the card no longer draws, so the dispatches go
        // too, not just their rendering. The digest asks for highlights and charts, nothing else.
        _mediator.Verify(m => m.Send(It.IsAny<GetOfficialRankingsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<GetWhatItTakesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
