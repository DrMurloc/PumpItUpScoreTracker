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
    IRequestHandler<FinalizeMatchCommand>,
    IRequestHandler<UpdateMatchScoresCommand>

{
    private readonly IAdminNotificationClient _admins;
    private readonly IBotClient _bot;
    private readonly IChartRepository _charts;
    private readonly IMatchRepository _matchRepository;
    private readonly IMediator _mediator;
    private readonly IQualifiersRepository _qualifiers;

    public MatchSaga(IMediator mediator, IMatchRepository matchRepository, IAdminNotificationClient admins,
        IChartRepository charts, IBotClient bot, IQualifiersRepository tournaments)
    {
        _matchRepository = matchRepository;
        _mediator = mediator;
        _charts = charts;
        _admins = admins;
        _bot = bot;
        _qualifiers = tournaments;
    }

    public async Task Handle(CreateMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatchLink(request.TournamentId, request.MatchLink, cancellationToken);
    }

    public async Task Handle(DeleteMatchLinkCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.DeleteMatchLink(request.TournamentId, request.FromName, request.ToName,
            cancellationToken);
    }

    public async Task Handle(DrawChartsCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var settings =
            await _matchRepository.GetRandomSettings(request.TournamentId, match.RandomSettings, cancellationToken);
        var charts = await _mediator.Send(new GetRandomChartsQuery(settings), cancellationToken);
        var newMatch = match with
        {
            ProtectedCharts = Array.Empty<Guid>(),
            VetoedCharts = Array.Empty<Guid>(),
            ActiveCharts = charts.Select(c => c.Id).ToArray(), State = MatchState.CardDraw
        };
        await _matchRepository.SaveMatch(request.TournamentId, newMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatch), cancellationToken);
    }

    public async Task Handle(FinalizeMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);

        var links = await _matchRepository.GetMatchLinksByFromMatchName(request.TournamentId, match.MatchName,
            cancellationToken);
        foreach (var link in links)
        {
            var nextMatch = await _matchRepository.GetMatch(request.TournamentId, link.ToMatch, cancellationToken);
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

            await _matchRepository.SaveMatch(request.TournamentId, nextMatch, cancellationToken);
            await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, nextMatch), cancellationToken);
        }

        var newMatchState = match with { State = MatchState.Completed };
        await _matchRepository.SaveMatch(request.TournamentId, newMatchState, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatchState), cancellationToken);


        try
        {
            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

            await _bot.SendMessage($@"# {newMatchState.MatchName} Completed! #
- {string.Join("\r\n- ", newMatchState.FinalPlaces.Select(p => $"{p} ({newMatchState.Points[p].Sum()})"))}",
                config.NotificationChannel,
                cancellationToken);
        }
        catch (Exception)
        {
            //Ignored
        }
    }

    public async Task Handle(FinishCardDrawCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var updatedMatch = match with
        {
            State = MatchState.Ready,
            Scores = match.Players.ToDictionary(p => p.ToString(),
                p => match.ActiveCharts.Select(c => PhoenixScore.From(0)).ToArray()),
            Points = match.Players.ToDictionary(p => p.ToString(), p => match.ActiveCharts.Select(c => 0).ToArray())
        };
        await _matchRepository.SaveMatch(request.TournamentId, updatedMatch, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, updatedMatch), cancellationToken);

        var charts = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.ActiveCharts,
            cancellationToken: cancellationToken);
        var vetoes = await _charts.GetCharts(MixEnum.Phoenix, chartIds: updatedMatch.VetoedCharts,
            cancellationToken: cancellationToken);
        try
        {
            var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

            await _bot.SendMessage($@"# {updatedMatch.MatchName} Card Draw Complete! #
({string.Join(", ", updatedMatch.Players)})
- {string.Join("\r\n- ", charts.Select(c => c.Song.Name + " " + c.DifficultyString))}
- {string.Join("\r\n- ", vetoes.Select(v => "~~" + v.Song.Name + " " + v.DifficultyString + "~~"))}",
                config.NotificationChannel,
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
        return await _matchRepository.GetAllMatches(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<(Name name, RandomSettings settings)>> Handle(GetAllRandomSettingsQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllRandomSettings(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksFromMatchQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchLinksByFromMatchName(request.TournamentId, request.FromMatchName,
            cancellationToken);
    }

    public async Task<IEnumerable<MatchLink>> Handle(GetMatchLinksQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetAllMatchLinks(request.TournamentId, cancellationToken);
    }

    public async Task<IEnumerable<MatchPlayer>> Handle(GetMatchPlayersQuery request,
        CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatchPlayers(request.TournamentId, cancellationToken);
    }

    public async Task<MatchView> Handle(GetMatchQuery request, CancellationToken cancellationToken)
    {
        return await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
    }

    public async Task Handle(PingMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);

        var message = $"The PIU TOs are requesting the players for {match.MatchName}";

        var playerDiscords = (await _matchRepository.GetMatchPlayers(request.TournamentId, cancellationToken))
            .ToDictionary(p => p.Name.ToString(), p => p.DiscordId, StringComparer.OrdinalIgnoreCase);
        if (match.State == MatchState.NotStarted && match.Players.All(p => !p.ToString().StartsWith("Unknown ")))
            message = $"Card draw is ready for {match.MatchName}";
        if (match.State == MatchState.Ready)
            message = $"{match.MatchName} is ready to play!";
        var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);

        await _bot.SendMessage(
            $@"{message}
{string.Join(", ", match.Players.Where(p => !p.ToString().StartsWith("Unknown ")).Select(p => $"<@{playerDiscords[p]}> ({p})"))}, please report to PIU TOs",
            config.NotificationChannel,
            cancellationToken);
    }

    public async Task Handle(ResolveMatchCommand request, CancellationToken cancellationToken)
    {
        var match = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var newMatchState = match.CalculatePoints();
        await _matchRepository.SaveMatch(request.TournamentId, newMatchState, cancellationToken);

        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, newMatchState), cancellationToken);
    }

    public async Task Handle(SaveRandomSettingsCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveRandomSettings(request.TournamentId, request.SettingsName, request.Settings,
            cancellationToken);
    }

    public async Task Handle(UpdateMatchCommand request, CancellationToken cancellationToken)
    {
        await _matchRepository.SaveMatch(request.TournamentId, request.NewView, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, request.NewView), cancellationToken);
    }

    public async Task Handle(UpdateMatchScoresCommand request, CancellationToken cancellationToken)
    {
        var oldMatch = await _matchRepository.GetMatch(request.TournamentId, request.MatchName, cancellationToken);
        var songWasComplete = oldMatch.Scores.Values.All(s => s[request.ChartIndex] > 0);
        var newScores = oldMatch.Scores.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
        newScores[request.Player][request.ChartIndex] = request.NewScore;
        var match = oldMatch with { Scores = newScores };
        match = match.CalculatePoints();

        await _matchRepository.SaveMatch(request.TournamentId, match, cancellationToken);
        await _mediator.Publish(new MatchUpdatedEvent(request.TournamentId, match), cancellationToken);
        if (songWasComplete) return;

        var songIsComplete = match.Scores.Values.All(s => s[request.ChartIndex] > 0);

        if (!songIsComplete) return;
        var chart = await _charts.GetChart(MixEnum.Phoenix, match.ActiveCharts[request.ChartIndex],
            cancellationToken);

        var config = await _qualifiers.GetQualifiersConfiguration(request.TournamentId, cancellationToken);
        var message = $@"**{match.MatchName}**
{chart.Song.Name} #DIFFICULTY|{chart.DifficultyString}# Completed:";
        foreach (var player in match.Players.OrderByDescending(p => match.Scores[p][request.ChartIndex]))
        {
            var score = match.Scores[player][request.ChartIndex];
            message +=
                @$"
- {player} - {score} #LETTERGRADE|{score.LetterGrade}#  ({match.Points[player][request.ChartIndex]} Points)";
        }

        await _bot.SendMessage(
            message,
            config.NotificationChannel,
            cancellationToken);
    }
}