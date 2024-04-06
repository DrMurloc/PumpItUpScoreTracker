using MediatR;
using ScoreTracker.Application.Commands;
using ScoreTracker.Application.Events;
using ScoreTracker.Application.Queries;
using ScoreTracker.Domain.Enums;
using ScoreTracker.Domain.Models;
using ScoreTracker.Domain.SecondaryPorts;
using ScoreTracker.Domain.ValueTypes;
using ScoreTracker.Domain.Views;

namespace ScoreTracker.Application.Handlers;

public sealed class MatchSaga : IRequestHandler<GetMatchQuery, MatchView>,
    IRequestHandler<UpdateMatchCommand>,
    IRequestHandler<DrawChartsCommand>,
    IRequestHandler<ResolveMatchCommand>,
    IRequestHandler<GetAllMatchesQuery, IEnumerable<MatchView>>,
    IRequestHandler<SaveRandomSettingsCommand>,
    IRequestHandler<GetAllRandomSettingsQuery, IEnumerable<(Name name, RandomSettings settings)>>,
    IRequestHandler<GetMatchLinksQuery, IEnumerable<MatchLink>>,
    IRequestHandler<GetMatchLinksFromMatchQuery, IEnumerable<MatchLink>>,
    IRequestHandler<CreateMatchLinkCommand>,
    IRequestHandler<DeleteMatchLinkCommand>,
    IRequestHandler<FinishCardDrawCommand>,
    IRequestHandler<PingMatchCommand>,
    IRequestHandler<GetMatchPlayersQuery, IEnumerable<MatchPlayer>>,
    IRequestHandler<FinalizeMatchCommand>

{
    private readonly IAdminNotificationClient _admins;
    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly IMatchRepository _matchRepository;
    private readonly IMediator _mediator;

    public MatchSaga(IMediator mediator, IMatchRepository matchRepository, IAdminNotificationClient admins,
        IChartRepository charts, IBotClient bot)
    {
        _matchRepository = matchRepository;
        _mediator = mediator;
        _charts = charts;
        _admins = admins;
        _bot = bot;
    }

    public async Task Handle(CreateMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatchLink(request.MatchLink, cancellationToken);
    }

    public async Task Handle(DeleteMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.DeleteMatchLink(request.FromName, request.ToName, cancellationToken);
    }

    public async Task Handle(DrawChartsCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);
        var settings = await _matchRepository.GetRandomSettings(match.RandomSettings, cancellationToken);
        var charts = await _mediator.Send(new GetRandomChartsQuery(settings), cancellationToken);
        var newMatch = match with
        {
            ActiveCharts = charts.Select(c => c.Id).ToArray(), State = MatchState.CardDraw
        };
        await _matchRepository.SaveMatch(newMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(newMatch), cancellationToken);
    }

    public async Task Handle(FinalizeMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);

        var links = await _matchRepository.GetMatchLinksByFromMatchName(match.MatchName, cancellationToken);
        foreach (var link in links)
        {
            var nextMatch = await _matchRepository.GetMatch(link.ToMatch, cancellationToken);
            var progressers = link.IsWinners
                ? match.FinalPlaces.Take(link.PlayerCount)
                : match.FinalPlaces.Reverse().Take(link.PlayerCount);

            foreach (var player in progressers)
            {
                if (nextMatch.Players.Contains(player)) continue;
                var nextIndex = nextMatch.Players.Select((p, i) => (p, i))
                    .Where(p => p.p.ToString().StartsWith("Unknown ", StringComparison.OrdinalIgnoreCase))
                    .Select(p => (int?)p.i)
                    .FirstOrDefault();
                if (nextIndex == null)
                {
                    await _admins.NotifyAdmin(
                        $"Player {player} Couldn't progress from {match.MatchName} to {nextMatch.MatchName}",
                        cancellationToken);
                    continue;
                }

                nextMatch.Players[nextIndex.Value] = player;
            }

            await _matchRepository.SaveMatch(nextMatch, cancellationToken);
            await _mediator.Publish(new MatchUpdatedEvent(nextMatch), cancellationToken);
        }

        var newMatchState = match with { State = MatchState.Completed };
        await _matchRepository.SaveMatch(newMatchState, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(newMatchState), cancellationToken);


        try
        {
            await _bot.PublishQualifiersMessage($@"# {newMatchState.MatchName} Completed! #
- {string.Join("\r\n- ", newMatchState.FinalPlaces.Select(p => $"{p} ({newMatchState.Points[p].Sum()})"))}",
                cancellationToken);
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    public async Task Handle(FinishCardDrawCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);
        var updatedMatch = match with
        {
            State = MatchState.Ready,
            Scores = match.Players.ToDictionary(p => p.ToString(),
                p => match.ActiveCharts.Select(c => PhoenixScore.From(0)).ToArray()),
            Points = match.Players.ToDictionary(p => p.ToString(), p => match.ActiveCharts.Select(c => 0).ToArray())
        };
        await _matchRepository.SaveMatch(updatedMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(updatedMatch), cancellationToken);

        var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.ActiveCharts,
            cancellationToken: cancellationToken);
        var vetoes = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.VetoedCharts,
            cancellationToken: cancellationToken);
        try
        {
            await _bot.PublishQualifiersMessage($@"# {updatedMatch.MatchName} Card Draw Complete! #
({string.Join(", ", updatedMatch.Players)})
- {string.Join("\r\n- ", charts.Select(c => c.Song.Name + " " + c.DifficultyString))}
- {string.Join("\r\n- ", vetoes.Select(v => "~~" + v.Song.Name + " " + v.DifficultyString + "~~"))}",
                cancellationToken);
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    public async Task<IEnumerable<MatchView>> Handle(GetAllMatchesQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllMatches(cancellationToken);
    }

    public async Task<IEnumerable<(Name name, RandomSettings settings)>> Handle(GetAllRandomSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllRandomSettings(cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksFromMatchQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchLinksByFromMatchName(request.FromMatchName, cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllMatchLinks(cancellationToken);
    }

    public async Task<IEnumerable<MatchPlayer>> Handle(GetMatchPlayersQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchPlayers(cancellationToken);
    }

    public async Task<MatchView> Handle(GetMatchQuery request, CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatch(request.MatchName, cancellationToken);
    }

    public async Task Handle(PingMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);

        var message = $"The PIU TOs are requesting the players for {match.MatchName}";

        var playerDiscords = (await _matchRepository.GetMatchPlayers(cancellationToken))
            .ToDictionary(p => p.Name.ToString(), p => p.DiscordId, StringComparer.OrdinalIgnoreCase);
        if (match.State == MatchState.NotStarted && match.Players.All(p => !p.ToString().StartsWith("Unknown ")))
            message = $"Card draw is ready for {match.MatchName}";
        if (match.State == MatchState.Ready)
            message = $"{match.MatchName} is ready to play!";
        await _bot.PublishQualifiersMessage(
            $@"{message}
{string.Join(", ", match.Players.Where(p => !p.ToString().StartsWith("Unknown ")).Select(p => $"<@{playerDiscords[p]}> ({p})"))}, please report to PIU TOs",
            cancellationToken);
    }

    public async Task Handle(ResolveMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.MatchName, cancellationToken);
        var scoring = match.Players.Length == 2
            ? new[] { 1, 0 }
            : match.Players.Select((p, i) => i + 1).OrderByDescending(i => i).ToArray();

        for (var chartIndex = 0; chartIndex < match.ActiveCharts.Length; chartIndex++)
        {
            var pointIndex = 0;
            foreach (var scoreGroup in match.Players.GroupBy(p => (int)match.Scores[p][chartIndex])
                         .OrderByDescending(g => g.Key))
            {
                var points = scoreGroup.Key == 0 ? 0 : scoring[pointIndex];
                foreach (var player in scoreGroup) match.Points[player][chartIndex] = points;

                pointIndex += scoreGroup.Count();
            }
        }

        var currentPosition = 0;

        foreach (var tie in match.Players.GroupBy(p => match.Points[p].Sum())
                     .OrderByDescending(g => g.Key))
        foreach (var tieBreakerResult in tie.OrderByDescending(name => match.Scores[name].Sum(s => s)))
            match.FinalPlaces[currentPosition++] = tieBreakerResult;

        var newMatchState = match with { State = MatchState.Finalizing };
        await _matchRepository.SaveMatch(newMatchState, cancellationToken);

        await _mediator.Publish(new MatchUpdatedEvent(newMatchState), cancellationToken);
    }

    public async Task Handle(SaveRandomSettingsCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveRandomSettings(request.SettingsName, request.Settings, cancellationToken);
    }

    public async Task Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatch(request.NewView, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.NewView), cancellationToken);
    }
}