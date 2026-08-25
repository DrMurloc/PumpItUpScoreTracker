using MassTransit;
using MediatR;
using ScoreTracker.Domain.Records;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.EventCompetition.Contracts;
using ScoreTracker.EventCompetition.Contracts.Events;
using ScoreTracker.EventCompetition.Contracts.Queries;
using ScoreTracker.SharedKernel.Enums;
using ScoreTracker.SharedKernel.Models;
using ScoreTracker.SharedKernel.ValueTypes;

namespace ScoreTracker.EventCompetition.Application;

/// <summary>
///     The MoM Discord card (march-of-murlocs.md §11.7): one card per published session —
///     a draft never fires one, and a published session cannot be edited, so no second card
///     ever corrects a first. The same per-mix, community-independent subscription as the
///     other feeds, read through the published IDiscordFeedReader port; composed as a
///     RichBotMessage so the Discord adapter owns the emoji swap and the plain-text
///     fallback. The board is named because a number without a chart type says nothing
///     (D15), placement rides the context line, and the biggest five rank by POINTS — the
///     currency the session is made of — never by raw score. A deleted session leaves its
///     card behind with a link that 404s (§10), accepted.
/// </summary>
internal sealed class MoMDiscordSaga : IConsumer<MoMSessionPublishedEvent>
{
    private const string SiteBase = "https://piuscores.arroweclip.se";

    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly IDiscordFeedReader _feeds;
    private readonly ILocalizedTextAccessor _localizer;
    private readonly IMediator _mediator;
    private readonly IUserReader _users;

    public MoMDiscordSaga(IBotClient bot, IDiscordFeedReader feeds, IMediator mediator,
        IChartRepository charts, IUserReader users, ILocalizedTextAccessor localizer)
    {
        _bot = bot;
        _feeds = feeds;
        _mediator = mediator;
        _charts = charts;
        _users = users;
        _localizer = localizer;
    }

    public async Task Consume(ConsumeContext<MoMSessionPublishedEvent> context)
    {
        var ct = context.CancellationToken;
        var session = await _mediator.Send(new GetMoMSessionQuery(context.Message.SessionId), ct);
        // Deleted between publish and delivery — the card simply never sends.
        if (session?.PublishedAt == null) return;

        var channels = await _feeds.GetSubscribedChannels(DiscordFeedKinds.MarchOfMurlocs,
            session.Mix, ct);
        if (channels.Count == 0) return;

        var board = await _mediator.Send(new GetMoMBoardQuery(session.BoardId), ct);
        var boardCount = board?.Rows.Count ?? 0;
        var charts = session.Charts.Count == 0
            ? new Dictionary<Guid, Chart>()
            : (await _charts.GetCharts(session.Mix,
                chartIds: session.Charts.Select(c => c.ChartId).Distinct().ToArray(),
                cancellationToken: ct)).ToDictionary(c => c.Id);
        var user = await _users.GetUser(session.UserId, ct);

        // One composition per registered language, fanned out to that language's channels.
        foreach (var group in channels.GroupBy(c => c.Culture))
        {
            var card = Card(session, boardCount, charts, user?.ProfileImage, group.Key);
            await _bot.SendRichMessages(new[] { card }, group.Select(c => c.ChannelId).ToArray(),
                ct);
        }
    }

    private RichBotMessage Card(MoMSessionView session, int boardCount,
        IReadOnlyDictionary<Guid, Chart> charts, Uri? avatar, string? culture)
    {
        var type = _localizer.Get(culture,
            session.ChartType == ChartType.Single ? "Singles" : "Doubles");
        var context = session.Place is { } place && boardCount > 0
            ? _localizer.Get(culture, "March of Murlocs · {0} · {1} · #{2} of {3}",
                session.Season.Name, type, place, boardCount)
            : _localizer.Get(culture, "March of Murlocs · {0} · {1}", session.Season.Name, type);
        var header = new RichBotSection(
            $"### #MIX|{session.Mix}# **{session.UserName}** — " +
            _localizer.Get(culture, "{0} points", session.TotalScore.ToString("N0")) +
            $"\n-# {context}",
            avatar);

        var grade = (PhoenixLetterGrade)Math.Clamp((int)Math.Floor(session.AverageGrade), 0,
            (int)PhoenixLetterGrade.SSSPlus);
        var stats = new RichBotText(string.Join(" · ",
            _localizer.Get(culture, "{0} charts", $"**{session.ChartsPlayed}**"),
            $"{_localizer.Get(culture, "avg lvl")} **{session.AverageDifficulty:F2}**",
            $"#DIFFICULTY|{session.ChartType.GetShortHand()}{session.LowestLevel}# → #DIFFICULTY|{session.ChartType.GetShortHand()}{session.HighestLevel}#",
            $"{_localizer.Get(culture, "downtime")} **{(int)session.RestTime.TotalMinutes}:{session.RestTime.Seconds:00}**",
            $"{_localizer.Get(culture, "Avg grade")} #LETTERGRADE|{grade}#"));

        // Ranked by points, not raw score — ranked by score this card would show the five
        // cleanest plays, which say nothing about the session (§11.7).
        var biggest = session.Charts
            .OrderByDescending(c => c.SessionScore)
            .Take(5)
            .Select(c =>
            {
                var name = charts.TryGetValue(c.ChartId, out var chart)
                    ? chart.Song.Name.ToString()
                    : "?";
                var chartGrade = ((PhoenixScore)c.Score).LetterGradeFor(session.Mix);
                return $"**{c.SessionScore:N0}** · #DIFFICULTY|{session.ChartType.GetShortHand()}{ChartLevel(charts, c)}# {name} · " +
                       $"#LETTERGRADE|{chartGrade}{(c.IsBroken ? "|True" : "")}# {c.Score:N0}";
            })
            .ToArray();

        return new RichBotMessage(header,
            new IRichBotBlock[]
            {
                stats,
                new RichBotDivider(),
                new RichBotText($"-# {_localizer.Get(culture, "Biggest five")}\n" +
                                string.Join("\n", biggest))
            },
            $"#MIX|{session.Mix}# {session.Mix.GetName()} · March of Murlocs",
            null,
            new[]
            {
                new RichBotLink(_localizer.Get(culture, "See the session"),
                    new Uri($"{SiteBase}/MarchOfMurlocs/Session/{session.Id}"))
            });
    }

    private static int ChartLevel(IReadOnlyDictionary<Guid, Chart> charts, MoMSessionChartRow row)
    {
        return charts.TryGetValue(row.ChartId, out var chart) ? (int)chart.Level : 0;
    }
}
